using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using ShipManagement.Api.Auth;
using ShipManagement.Domain.Errors;

namespace ShipManagement.Api.Errors;

/// <summary>
/// Maps exceptions to problem documents in one place: controllers contain no try/catch.
/// Expected business errors keep their curated message; anything unexpected becomes a generic 500
/// whose details are only in the server log, correlated by traceId (SEC-08).
/// </summary>
internal sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; the cancellation already stopped the database work (SEC-10).
            return true;
        }

        var (status, code, detail, errors) = exception switch
        {
            RequestValidationException e => (StatusCodes.Status400BadRequest, e.Code, e.Message, e.Errors),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Code, e.Message, null),
            ConflictException e => (StatusCodes.Status409Conflict, e.Code, e.Message, null),
            ShipInactiveException e => (StatusCodes.Status409Conflict, e.Code, e.Message, null),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Code, e.Message, null),
            PreconditionFailedException e => (StatusCodes.Status412PreconditionFailed, e.Code, e.Message, null),
            DependencyUnavailableException e => (StatusCodes.Status503ServiceUnavailable, e.Code, e.Message, null),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge, ErrorCodes.PayloadTooLarge, "The request body is too large.", null),
            BadHttpRequestException e => (e.StatusCode, ErrorCodes.ValidationFailed, "The request could not be read.", null),
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError,
                  "An unexpected error occurred. Quote the traceId when reporting this problem.", (IReadOnlyDictionary<string, string[]>?)null),
        };

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        if (status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception, traceId);
        }
        else if (exception is DependencyUnavailableException unavailable)
        {
            // Tell well-behaved clients when to come back; keep the real cause in the log only (SEC-08).
            httpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(unavailable.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            LogDependencyUnavailable(logger, unavailable.InnerException ?? unavailable, traceId);
        }
        else if (status == StatusCodes.Status403Forbidden)
        {
            // Authorization denials are security events (SEC-09).
            LogAccessDenied(logger, httpContext.User.FindFirstValue(AppClaimTypes.UserId) ?? "anonymous", httpContext.Request.Method, traceId);
        }

        await problemDetails.WriteProblemAsync(httpContext, status, code, detail, errors);
        return true;
    }

    [LoggerMessage(EventId = 9101, Level = LogLevel.Error, Message = "Unhandled exception (traceId {TraceId})")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string traceId);

    [LoggerMessage(EventId = 9103, Level = LogLevel.Warning, Message = "Database unavailable (traceId {TraceId})")]
    private static partial void LogDependencyUnavailable(ILogger logger, Exception exception, string traceId);

    [LoggerMessage(EventId = 9102, Level = LogLevel.Warning,
        Message = "Access denied for user {UserId} on {Method} request (traceId {TraceId})")]
    private static partial void LogAccessDenied(ILogger logger, string userId, string method, string traceId);
}
