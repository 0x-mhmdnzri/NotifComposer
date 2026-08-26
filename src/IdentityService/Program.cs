using FluentValidation;
using FluentValidation.AspNetCore;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Services;
using IdentityService.Application.Validators;
using IdentityService.GrpcServices;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Middleware;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.IO.Compression;

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

    // Response compression reduces payload transfer time on larger list responses (hide network cost)
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

    // Short-lived output cache for hot read endpoints (hide repeated DB latency)
    builder.Services.AddOutputCache(options =>
    {
        options.AddBasePolicy(b => b.Expire(TimeSpan.FromSeconds(10)).Tag("users"));
        options.AddPolicy("UserById", b => b.Expire(TimeSpan.FromSeconds(30)).SetVaryByRouteValue("id").Tag("users"));
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    // Pooling + timeouts on the connection string (reduce queueing under load)
    builder.Services.AddDbContext<IdentityDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.CommandTimeout(5); // hard bound on DB stage latency
        }));

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

    // Order: latency outermost so it includes everything
    app.UseMiddleware<LatencyMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseResponseCompression();
    app.UseOutputCache();

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
