using System.Net;
using System.Text.Json;

namespace IdentityService.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = exception switch
        {
            InvalidOperationException => HttpStatusCode.BadRequest,
            ArgumentException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };

        var result = JsonSerializer.Serialize(new
        {
            error = exception.Message,
            statusCode = (int)code
        });

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        return context.Response.WriteAsync(result);
    }
}
