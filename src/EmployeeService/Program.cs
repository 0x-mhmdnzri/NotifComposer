using EmployeeService.Application.Services;
using EmployeeService.Application.Validators;
using EmployeeService.Infrastructure.Clients;
using EmployeeService.Infrastructure.Persistence;
using EmployeeService.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<CreateEmployeeValidator>();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    builder.Services.AddDbContext<EmployeeDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Identity gRPC
    builder.Services.Configure<IdentityGrpcOptions>(options =>
    {
        options.GrpcAddress = builder.Configuration["IdentityService:GrpcAddress"]
            ?? "http://localhost:5002";
    });
    builder.Services.AddSingleton<IdentityGrpcClient>();

    // Notification HTTP client
    builder.Services.Configure<NotificationOptions>(options =>
    {
        options.BaseUrl = builder.Configuration["NotificationService:BaseUrl"]
            ?? "http://localhost:5005";
    });
    builder.Services.AddHttpClient<NotificationHttpClient>();

    builder.Services.AddScoped<EmployeeAppService>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Employee Service API", Version = "v1" });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();
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
