using EmployeeService.Application.Interfaces;
using EmployeeService.Application.Services;
using EmployeeService.Application.Validators;
using EmployeeService.Infrastructure.Clients;
using EmployeeService.Infrastructure.Persistence;
using EmployeeService.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.IO.Compression;
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

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    builder.Services.AddDbContext<EmployeeDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.CommandTimeout(5);
        }));

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
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();
        db.Database.Migrate();
        try { db.Database.EnsureCreated(); } catch { /* already migrated */ }
    }

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
