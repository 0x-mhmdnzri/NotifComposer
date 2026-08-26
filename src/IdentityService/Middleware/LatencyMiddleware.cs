using System.Diagnostics;

namespace IdentityService.Middleware;

/// <summary>
/// Measures request latency end-to-end (receive → response).
/// Logs distribution-aware signals: duration + status, and flags slow requests
/// (threshold chosen as a rough p99 target for this service, not the average).
/// </summary>
public sealed class LatencyMiddleware
{
    // Target: most requests under this; log when exceeded so we can attribute tail latency.
    private static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromMilliseconds(200);

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

            // Structured log so log aggregators can compute p50/p95/p99 later
            _logger.LogInformation(
                "HTTP {Method} {Path} → {StatusCode} in {ElapsedMs:F1}ms",
                method, path, status, elapsedMs);

            if (sw.Elapsed >= SlowRequestThreshold)
            {
                _logger.LogWarning(
                    "SLOW request (above {ThresholdMs}ms target): {Method} {Path} took {ElapsedMs:F1}ms Status={StatusCode}",
                    SlowRequestThreshold.TotalMilliseconds, method, path, elapsedMs, status);
            }

            // Expose timing to clients / load generators without coordinated omission tricks
            context.Response.Headers["X-Response-Time-Ms"] = elapsedMs.ToString("F1");
        }
    }
}
