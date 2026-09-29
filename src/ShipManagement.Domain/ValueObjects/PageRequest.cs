namespace ShipManagement.Domain.ValueObjects;

/// <summary>Paging rules shared by every list endpoint (D-16).</summary>
public static class PageRequest
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Upper bound that keeps OFFSET arithmetic far from overflow; no real list is this long.</summary>
    public const int MaxPageNumber = 1_000_000;
}
