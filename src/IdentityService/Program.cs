using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Services;
using IdentityService.Application.Validators;
using IdentityService.GrpcServices;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Middleware;
using IdentityService.Security;
using Microsoft.AspNetCore.Authentication;
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

    builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
    builder.Services.AddAuthentication(ApiKeyOptions.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyOptions.SchemeName, null);
    builder.Services.AddAuthorization();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("api", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        options.AddPolicy("write", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

    builder.Services.AddOutputCache(options =>
    {
        options.AddBasePolicy(b => b.Expire(TimeSpan.FromSeconds(10)).Tag("users"));
        options.AddPolicy("UserById", b => b.Expire(TimeSpan.FromSeconds(30)).SetVaryByRouteValue("id").Tag("users"));
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("default", policy =>
        {
            var origins = builder.Configuration["Security:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          ?? Array.Empty<string>();
            if (origins.Length == 0)
                policy.SetIsOriginAllowed(_ => false); // deny all browser origins by default
            else
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        });
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    builder.Services.AddDbContext<IdentityDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(5)));

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<UserAppService>();

    builder.Services.AddGrpc();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Identity Service API", Version = "v1" });
        c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "API Key via X-Api-Key header",
            Name = "X-Api-Key",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Database.Migrate();
    }

    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<LatencyMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseResponseCompression();
    app.UseRateLimiter();
    app.UseCors("default");
    app.UseOutputCache();

    var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());
    if (swaggerEnabled)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGrpcService<UserGrpcService>();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

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
