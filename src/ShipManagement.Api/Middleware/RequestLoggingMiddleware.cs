using System.Diagnostics;
using System.Security.Claims;
using ShipManagement.Api.Auth;

namespace ShipManagement.Api.Middleware;

/// <summary>
/// One structured log line per request: method, route template, status, duration and caller id.
/// The route template is logged instead of the raw path, and the query string and body are never logged,
/// because a crew search term may contain a person's name (SEC-09).
/// </summary>
internal sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var userId = context.User.FindFirstValue(AppClaimTypes.UserId) ?? "anonymous";

            if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
            {
                LogHealthRequest(logger, route, context.Response.StatusCode, elapsedMs);
            }
            else
            {
                LogRequest(logger, context.Request.Method, route, context.Response.StatusCode, elapsedMs, userId);
            }
        }
    }

    [LoggerMessage(EventId = 9201, Level = LogLevel.Information,
        Message = "HTTP {Method} {Route} responded {StatusCode} in {ElapsedMs:0.0} ms for user {UserId}")]
    private static partial void LogRequest(ILogger logger, string method, string route, int statusCode, double elapsedMs, string userId);

    [LoggerMessage(EventId = 9202, Level = LogLevel.Debug,
        Message = "Health probe {Route} responded {StatusCode} in {ElapsedMs:0.0} ms")]
    private static partial void LogHealthRequest(ILogger logger, string route, int statusCode, double elapsedMs);
}
