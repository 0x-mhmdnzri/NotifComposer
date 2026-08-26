using System.IO.Compression;
using System.Threading.RateLimiting;
using EmployeeService.Application.Interfaces;
using EmployeeService.Application.Services;
using EmployeeService.Application.Validators;
using EmployeeService.Infrastructure.Clients;
using EmployeeService.Infrastructure.Persistence;
using EmployeeService.Middleware;
using EmployeeService.Security;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
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
    builder.Services.AddValidatorsFromAssemblyContaining<CreateEmployeeValidator>();

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
        options.AddBasePolicy(b => b.Expire(TimeSpan.FromSeconds(10)).Tag("employees"));
        options.AddPolicy("EmployeeById", b => b.Expire(TimeSpan.FromSeconds(30)).SetVaryByRouteValue("id").Tag("employees"));
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("default", policy =>
        {
            var origins = builder.Configuration["Security:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          ?? Array.Empty<string>();
            if (origins.Length == 0)
                policy.SetIsOriginAllowed(_ => false);
            else
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        });
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    builder.Services.AddDbContext<EmployeeDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(5)));

    builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
    builder.Services.AddScoped<EmployeeAppService>();

    builder.Services.Configure<IdentityGrpcOptions>(options =>
    {
        options.GrpcAddress = builder.Configuration["IdentityService:GrpcAddress"] ?? "http://localhost:5002";
        if (int.TryParse(builder.Configuration["IdentityService:TimeoutMilliseconds"], out var t))
            options.TimeoutMilliseconds = t;
    });
    builder.Services.AddSingleton<IIdentityClient, IdentityGrpcClient>();

    var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

    builder.Services.AddTransactionalBox(x =>
    {
        x.AddOutbox(
            storage => storage.UseEntityFrameworkCore<EmployeeDbContext>(),
            transport => transport.UseKafka(settings => settings.BootstrapServers = kafkaBootstrap));
    },
    settings => settings.ServiceId = "EmployeeService");

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Employee Service API", Version = "v1" });
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
        var db = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();
        db.Database.Migrate();
        try { db.Database.EnsureCreated(); } catch { /* already migrated */ }
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
