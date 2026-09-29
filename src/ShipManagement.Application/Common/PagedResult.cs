namespace ShipManagement.Application.Common;

/// <summary>A page of results plus the information a client needs to page through all of them (D-16).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
