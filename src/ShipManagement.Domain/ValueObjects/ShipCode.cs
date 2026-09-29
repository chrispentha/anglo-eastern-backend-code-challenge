using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ShipManagement.Domain.ValueObjects;

/// <summary>
/// A ship's business identifier: 3-10 upper-case letters or digits (D-12).
/// Input is trimmed and upper-cased, so "ship01" and "SHIP01" are the same ship.
/// </summary>
public sealed partial record ShipCode
{
    public const int MinLength = 3;
    public const int MaxLength = 10;

    private ShipCode(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse([NotNullWhen(true)] string? input, [NotNullWhen(true)] out ShipCode? shipCode)
    {
        shipCode = null;
        if (input is null)
        {
            return false;
        }

        var normalized = input.Trim().ToUpperInvariant();
        if (!Pattern().IsMatch(normalized))
        {
            return false;
        }

        shipCode = new ShipCode(normalized);
        return true;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9]{3,10}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();
}
