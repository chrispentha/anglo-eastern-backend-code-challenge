namespace ShipManagement.Domain.ValueObjects;

/// <summary>
/// Whitelisted sort keys for the crew list (D-15). Values reach the stored procedure only after they
/// have been matched against this fixed set, never as free text (SEC-06).
/// </summary>
public static class CrewListSort
{
    public const string DefaultSortBy = "rank";

    /// <summary>Allowed sort keys, in their canonical spelling.</summary>
    public static readonly IReadOnlyList<string> SortKeys =
        ["rank", "crewMemberId", "firstName", "lastName", "age", "nationality", "signOnDate", "status"];

    /// <summary>Returns the canonical key for a case-insensitive match, the default when empty, or null when not allowed.</summary>
    public static string? NormalizeSortBy(string? sortBy) =>
        string.IsNullOrWhiteSpace(sortBy)
            ? DefaultSortBy
            : SortKeys.FirstOrDefault(k => string.Equals(k, sortBy.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Whitelisted sort directions.</summary>
public static class SortDirection
{
    public const string Ascending = "asc";
    public const string Descending = "desc";

    /// <summary>Returns "asc" or "desc" for a case-insensitive match, "asc" when empty, or null when not allowed.</summary>
    public static string? Normalize(string? direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            return Ascending;
        }

        var trimmed = direction.Trim();
        if (string.Equals(trimmed, Ascending, StringComparison.OrdinalIgnoreCase))
        {
            return Ascending;
        }

        return string.Equals(trimmed, Descending, StringComparison.OrdinalIgnoreCase) ? Descending : null;
    }
}
