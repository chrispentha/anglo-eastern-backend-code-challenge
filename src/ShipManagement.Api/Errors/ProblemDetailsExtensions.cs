using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShipManagement.Domain.Errors;

namespace ShipManagement.Api.Errors;

/// <summary>
/// Every error response is an RFC 9457 problem document with a stable <c>code</c> and a <c>traceId</c>
/// that support staff can look up in the logs. No stack traces, SQL text or server details (SEC-08).
/// </summary>
internal static class ProblemDetailsExtensions
{
    public const string CodeKey = "code";
    public const string TraceIdKey = "traceId";
    public const string ErrorsKey = "errors";

    public static Task WriteProblemAsync(
        this IProblemDetailsService service,
        HttpContext httpContext,
        int statusCode,
        string code,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails { Status = statusCode, Detail = detail };
        problem.Extensions[CodeKey] = code;
        if (errors is { Count: > 0 })
        {
            problem.Extensions[ErrorsKey] = errors;
        }

        return service.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem }).AsTask();
    }

    /// <summary>Fills in defaults for problem documents produced anywhere in the pipeline (status pages, 415, etc.).</summary>
    public static void AddDefaults(ProblemDetailsContext context)
    {
        var extensions = context.ProblemDetails.Extensions;
        extensions[TraceIdKey] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        extensions.TryAdd(CodeKey, context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
            StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
            StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
            StatusCodes.Status404NotFound => ErrorCodes.NotFound,
            StatusCodes.Status409Conflict => ErrorCodes.Conflict,
            StatusCodes.Status412PreconditionFailed => ErrorCodes.PreconditionFailed,
            StatusCodes.Status413PayloadTooLarge => ErrorCodes.PayloadTooLarge,
            StatusCodes.Status415UnsupportedMediaType => ErrorCodes.UnsupportedMediaType,
            StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
            StatusCodes.Status503ServiceUnavailable => ErrorCodes.DatabaseUnavailable,
            _ => ErrorCodes.InternalError,
        });
    }
}
