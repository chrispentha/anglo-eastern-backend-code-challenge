namespace ShipManagement.Domain.Errors;

/// <summary>
/// Stable, machine-readable error codes returned in the <c>code</c> field of every error response.
/// Clients branch on these, never on human-readable messages.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ConstraintViolation = "CONSTRAINT_VIOLATION";
    public const string NotFound = "NOT_FOUND";
    public const string ShipNotFound = "SHIP_NOT_FOUND";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string ApiKeyNotFound = "API_KEY_NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string ShipInactive = "SHIP_INACTIVE";
    public const string PreconditionFailed = "PRECONDITION_FAILED";
    public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string RateLimited = "RATE_LIMITED";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string InternalError = "INTERNAL_ERROR";
}
