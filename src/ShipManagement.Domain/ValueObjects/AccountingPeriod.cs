using System.Globalization;
using System.Text.RegularExpressions;

namespace ShipManagement.Domain.ValueObjects;

/// <summary>
/// A monthly accounting period, written <c>yyyy-MM</c> in the API (D-10) and stored as the first day of
/// the month in the database. Accepted range 2000-01 .. 2099-12.
/// </summary>
public readonly partial record struct AccountingPeriod
{
    public const int MinYear = 2000;
    public const int MaxYear = 2099;

    private AccountingPeriod(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    /// <summary>First day of the month, the database representation.</summary>
    public DateOnly FirstDay => new(Year, Month, 1);

    /// <summary>Strictly parses <c>yyyy-MM</c> (e.g. "2025-07"); "2025-7", "2025-13" and "07-2025" are rejected.</summary>
    public static bool TryParse(string? input, out AccountingPeriod period)
    {
        period = default;
        if (input is null || !Pattern().IsMatch(input))
        {
            return false;
        }

        var year = int.Parse(input.AsSpan(0, 4), CultureInfo.InvariantCulture);
        var month = int.Parse(input.AsSpan(5, 2), CultureInfo.InvariantCulture);
        if (year is < MinYear or > MaxYear)
        {
            return false;
        }

        period = new AccountingPeriod(year, month);
        return true;
    }

    /// <summary>Parses input that has already been validated; throws <see cref="FormatException"/> otherwise.</summary>
    public static AccountingPeriod Parse(string input) =>
        TryParse(input, out var period)
            ? period
            : throw new FormatException("Accounting period must be in yyyy-MM format between 2000-01 and 2099-12.");

    public static AccountingPeriod FromDate(DateOnly date) => new(date.Year, date.Month);

    /// <summary>"2025-07".</summary>
    public override string ToString() => FirstDay.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>"Jul 2025", independent of the server culture.</summary>
    public string ToLabel() => FirstDay.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();
}
