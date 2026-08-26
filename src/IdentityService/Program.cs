using FluentValidation;
using FluentValidation.AspNetCore;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Services;
using IdentityService.Application.Validators;
using IdentityService.GrpcServices;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Middleware;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(8080);
        options.ListenAnyIP(8081, o => o.Protocols = HttpProtocols.Http2);
    });

    builder.Services.AddControllers();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<CreateUserValidator>();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    builder.Services.AddDbContext<IdentityDbContext>(options =>
        options.UseNpgsql(connectionString));

    // DIP: depend on abstractions
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<UserAppService>();

    builder.Services.AddGrpc();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Identity Service API", Version = "v1" });
    });

    var app = builder.Build();

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
