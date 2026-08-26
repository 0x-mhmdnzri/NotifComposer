using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Application.Validators;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Middleware;
using Serilog;
using TransactionalBox;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<CreateNotificationValidator>();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseNpgsql(connectionString));

    builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
    builder.Services.AddScoped<NotificationAppService>();

    var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

    builder.Services.AddTransactionalBox(x =>
    {
        x.AddInbox(
            storage => storage.UseEntityFrameworkCore<NotificationDbContext>(),
            transport => transport.UseKafka(s => s.BootstrapServers = kafkaBootstrap),
            assembly: typeof(Program).Assembly);
    },
    settings => settings.ServiceId = "NotificationService");

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Notification Service API", Version = "v1" });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        db.Database.Migrate();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
