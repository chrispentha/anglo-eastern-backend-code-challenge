using FluentValidation;
using ShipManagement.Domain.Errors;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Application.Common;

public static class ValidationExtensions
{
    /// <summary>Validates and throws <see cref="RequestValidationException"/> listing every failing field (camelCase).</summary>
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        throw new RequestValidationException(errors);
    }

    public static IRuleBuilderOptions<T, int?> ValidPageNumber<T>(this IRuleBuilder<T, int?> rule) =>
        rule.InclusiveBetween(1, PageRequest.MaxPageNumber).WithMessage(ValidationMessages.PageNumber);

    public static IRuleBuilderOptions<T, int?> ValidPageSize<T>(this IRuleBuilder<T, int?> rule) =>
        rule.InclusiveBetween(1, PageRequest.MaxPageSize).WithMessage(ValidationMessages.PageSize);

    public static IRuleBuilderOptions<T, string?> ValidShipCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(code => ShipCode.TryParse(code, out _)).WithMessage(ValidationMessages.ShipCode);

    /// <summary>Parses a ship code taken from the route, or throws a 400 naming the field.</summary>
    public static string ParseShipCode(string? raw) =>
        ShipCode.TryParse(raw, out var code)
            ? code.Value
            : throw RequestValidationException.ForField("shipCode", ValidationMessages.ShipCode);

    /// <summary>Ensures an id taken from the route is positive, or throws a 400 naming the field.</summary>
    public static void EnsurePositiveId(int id, string field)
    {
        if (id <= 0)
        {
            throw RequestValidationException.ForField(field, $"{field} must be a positive integer.");
        }
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
