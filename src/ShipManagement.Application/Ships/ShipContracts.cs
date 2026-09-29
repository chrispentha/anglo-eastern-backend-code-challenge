using ShipManagement.Application.Common;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Application.Ships;

/// <summary>
/// A ship. <see cref="Version"/> changes on every update; send it back in If-Match to update safely (D-30).
/// </summary>
public sealed record ShipDto(
    string ShipCode, string ShipName, string FiscalYearCode, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string Version);

/// <summary>Request body for creating a ship (US-03).</summary>
public sealed class CreateShipRequest
{
    /// <summary>Unique code, 3-10 letters or digits (stored upper-case).</summary>
    public string? ShipCode { get; init; }

    /// <summary>Ship name, 1-100 characters.</summary>
    public string? ShipName { get; init; }

    /// <summary>Fiscal year code MMNN, e.g. 0112 (Jan-Dec) or 0403 (Apr-Mar).</summary>
    public string? FiscalYearCode { get; init; }

    /// <summary>Active (default) or Inactive.</summary>
    public string? Status { get; init; }
}

/// <summary>Request body for renaming or (de)activating a ship. The fiscal year is immutable (D-23).</summary>
public sealed class UpdateShipRequest
{
    public string? ShipName { get; init; }

    public string? Status { get; init; }
}

/// <summary>Query string for listing ships (US-04).</summary>
public sealed class ListShipsQuery
{
    public int? PageNumber { get; init; }

    public int? PageSize { get; init; }

    /// <summary>Optional status filter: Active or Inactive.</summary>
    public string? Status { get; init; }
}

/// <summary>Ship data access; every method maps to one stored procedure.</summary>
public interface IShipRepository
{
    Task<ShipDto> CreateAsync(
        string shipCode, string shipName, string fiscalYearCode, string? status, int requestedByUserId, CancellationToken cancellationToken);

    Task<ShipDto> GetByCodeAsync(string shipCode, int requestedByUserId, CancellationToken cancellationToken);

    Task<PagedResult<ShipDto>> ListAsync(
        int pageNumber, int pageSize, string? status, int requestedByUserId, CancellationToken cancellationToken);

    /// <summary>Updates the ship; when <paramref name="expectedVersion"/> is set, only if it is still current.</summary>
    Task<ShipDto> UpdateAsync(
        string shipCode, string? shipName, string? status, RowVersion? expectedVersion, int requestedByUserId, CancellationToken cancellationToken);
}
