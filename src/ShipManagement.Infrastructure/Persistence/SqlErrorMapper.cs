using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using ShipManagement.Domain.Errors;

namespace ShipManagement.Infrastructure.Persistence;

/// <summary>
/// Translates SQL Server errors into domain exceptions. Stored procedures raise business errors with
/// <c>THROW 5000x, 'CODE|message'</c>; only that curated message ever reaches the client. Any other
/// SQL error is left unmapped and surfaces as a generic 500 without SQL details (SEC-08).
/// </summary>
public static class SqlErrorMapper
{
    public const int ValidationFailed = 50001;
    public const int NotFound = 50002;
    public const int Conflict = 50003;
    public const int ShipInactive = 50004;
    public const int Forbidden = 50005;
    public const int PreconditionFailed = 50006;
    public const int UniqueConstraintViolation = 2627;
    public const int UniqueIndexViolation = 2601;
    public const int ConstraintViolation = 547;

    public static bool TryMap(SqlException exception, [NotNullWhen(true)] out DomainException? mapped) =>
        TryMap(exception.Number, exception.Message, out mapped);

    public static bool TryMap(int errorNumber, string message, [NotNullWhen(true)] out DomainException? mapped)
    {
        var (code, text) = Split(message);

        mapped = errorNumber switch
        {
            ValidationFailed => new RequestValidationException(code ?? ErrorCodes.ValidationFailed, text),
            NotFound => new NotFoundException(code ?? ErrorCodes.NotFound, text),
            Conflict => new ConflictException(code ?? ErrorCodes.Conflict, text),
            ShipInactive => new ShipInactiveException(text),
            Forbidden => new ForbiddenException(text),
            PreconditionFailed => new PreconditionFailedException(text),
            UniqueConstraintViolation or UniqueIndexViolation =>
                new ConflictException(ErrorCodes.Conflict, "A record with the same unique key already exists."),
            ConstraintViolation =>
                new RequestValidationException(ErrorCodes.ConstraintViolation, "The request violates a data integrity rule."),
            _ => null,
        };

        return mapped is not null;
    }

    /// <summary>Splits "CODE|message"; a message without a well-formed code prefix is returned whole.</summary>
    internal static (string? Code, string Message) Split(string message)
    {
        var separator = message.IndexOf('|', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return (null, message);
        }

        var code = message[..separator];
        return code.All(c => c is (>= 'A' and <= 'Z') or '_')
            ? (code, message[(separator + 1)..])
            : (null, message);
    }
}
