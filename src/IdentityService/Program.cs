using FluentValidation;
using FluentValidation.AspNetCore;
using IdentityService.Application.Services;
using IdentityService.Application.Validators;
using IdentityService.GrpcServices;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Controllers + Validation
    builder.Services.AddControllers();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<CreateUserValidator>();

    // EF Core
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    builder.Services.AddDbContext<IdentityDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Application services
    builder.Services.AddScoped<UserAppService>();

    // gRPC
    builder.Services.AddGrpc();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Identity Service API", Version = "v1" });
    });

    var app = builder.Build();

    // Auto-migrate
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Database.Migrate();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();
    app.MapGrpcService<UserGrpcService>();

    // Health for docker
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
