using System.Net;
using System.Text.Json;

namespace EmployeeService.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (code, clientMessage) = exception switch
        {
            InvalidOperationException or ArgumentException =>
                (HttpStatusCode.BadRequest, exception.Message),
            KeyNotFoundException =>
                (HttpStatusCode.NotFound, "Resource not found."),
            UnauthorizedAccessException =>
                (HttpStatusCode.Unauthorized, "Unauthorized."),
            _ =>
                (HttpStatusCode.InternalServerError,
                    _env.IsDevelopment()
                        ? "An unexpected error occurred. See server logs for details."
                        : "An unexpected error occurred.")
        };

        var result = JsonSerializer.Serialize(new
        {
            error = clientMessage,
            statusCode = (int)code,
            traceId = context.TraceIdentifier
        });

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        return context.Response.WriteAsync(result);
    }
}
