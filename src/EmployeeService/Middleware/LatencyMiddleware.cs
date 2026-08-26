using System.Diagnostics;

namespace EmployeeService.Middleware;

/// <summary>
/// End-to-end request latency measurement. Flags requests above the service latency budget
/// so tail latency is visible in logs (not hidden behind averages).
/// </summary>
public sealed class LatencyMiddleware
{
    // Employee create includes a sync gRPC hop → budget higher than pure CRUD
    private static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromMilliseconds(400);

    private readonly RequestDelegate _next;
    private readonly ILogger<LatencyMiddleware> _logger;

    public LatencyMiddleware(RequestDelegate next, ILogger<LatencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var elapsedMs = sw.Elapsed.TotalMilliseconds;
            var path = context.Request.Path.Value ?? "/";
            var method = context.Request.Method;
            var status = context.Response.StatusCode;

            _logger.LogInformation(
                "HTTP {Method} {Path} → {StatusCode} in {ElapsedMs:F1}ms",
                method, path, status, elapsedMs);

            if (sw.Elapsed >= SlowRequestThreshold)
            {
                _logger.LogWarning(
                    "SLOW request (above {ThresholdMs}ms budget): {Method} {Path} took {ElapsedMs:F1}ms Status={StatusCode}",
                    SlowRequestThreshold.TotalMilliseconds, method, path, elapsedMs, status);
            }

            context.Response.Headers["X-Response-Time-Ms"] = elapsedMs.ToString("F1");
        }
    }
}
