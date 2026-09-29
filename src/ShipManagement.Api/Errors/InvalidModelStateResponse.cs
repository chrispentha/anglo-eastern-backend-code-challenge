using Microsoft.AspNetCore.Mvc;
using ShipManagement.Domain.Errors;

namespace ShipManagement.Api.Errors;

/// <summary>
/// Shapes model-binding failures (malformed JSON, unknown JSON properties, non-numeric query values, missing body)
/// exactly like the validation errors raised by the services: 400, code VALIDATION_FAILED, errors by field.
/// Written through <see cref="IProblemDetailsService"/> (not an ObjectResult) so content negotiation and
/// [Produces] cannot change the application/problem+json media type.
/// </summary>
internal static class InvalidModelStateResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => NormalizeKey(entry.Key),
                entry => entry.Value!.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "The value is invalid." : e.ErrorMessage)
                    .ToArray(),
                StringComparer.Ordinal);

        return new ProblemResult(errors);
    }

    /// <summary>"$.fullName" / "FullName" / "" become "fullName" / "fullName" / "body".</summary>
    internal static string NormalizeKey(string key)
    {
        var trimmed = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key.TrimStart('$');
        if (trimmed.Length == 0)
        {
            return "body";
        }

        return char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
    }

    private sealed class ProblemResult(IReadOnlyDictionary<string, string[]> errors) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context) =>
            context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteProblemAsync(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationFailed,
                "One or more validation errors occurred.",
                errors);
    }
}
