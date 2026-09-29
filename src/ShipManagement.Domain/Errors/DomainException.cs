namespace ShipManagement.Domain.Errors;

/// <summary>
/// Base type for expected business errors. Each subtype maps to exactly one HTTP status code
/// in the API's exception handler; the message is safe to show to API clients.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    protected DomainException(string code, string message, Exception? innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Machine-readable error code (see <see cref="ErrorCodes"/>).</summary>
    public string Code { get; }
}

/// <summary>The request is invalid (HTTP 400). <see cref="Errors"/> lists every failing field.</summary>
public sealed class RequestValidationException : DomainException
{
    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(ErrorCodes.ValidationFailed, "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public RequestValidationException(string code, string message)
        : base(code, message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Convenience factory for a single failing field.</summary>
    public static RequestValidationException ForField(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>The resource does not exist or the caller may not know it exists (HTTP 404).</summary>
public sealed class NotFoundException(string code, string message) : DomainException(code, message);

/// <summary>The request conflicts with current state, e.g. a duplicate key (HTTP 409).</summary>
public sealed class ConflictException(string code, string message) : DomainException(code, message);

/// <summary>An operational query targeted an inactive ship (HTTP 409, code SHIP_INACTIVE).</summary>
public sealed class ShipInactiveException(string message) : DomainException(ErrorCodes.ShipInactive, message);

/// <summary>The caller is authenticated but not allowed to perform the operation (HTTP 403).</summary>
public sealed class ForbiddenException(string message) : DomainException(ErrorCodes.Forbidden, message);

/// <summary>
/// An If-Match precondition failed: the resource changed since the caller read it (HTTP 412, D-30).
/// </summary>
public sealed class PreconditionFailedException(string message) : DomainException(ErrorCodes.PreconditionFailed, message);

/// <summary>
/// A dependency (the database) is temporarily unavailable: transient failure after retries, or the circuit breaker
/// is open (HTTP 503 with Retry-After, D-31). The inner exception is kept for the server log only.
/// </summary>
public sealed class DependencyUnavailableException(string message, TimeSpan retryAfter, Exception? innerException = null)
    : DomainException(ErrorCodes.DatabaseUnavailable, message, innerException)
{
    /// <summary>How long the client should wait before retrying.</summary>
    public TimeSpan RetryAfter { get; } = retryAfter;
}
