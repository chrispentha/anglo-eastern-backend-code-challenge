using FluentValidation;
using Microsoft.Extensions.Logging;
using ShipManagement.Application.Common;
using ShipManagement.Domain.Errors;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Application.Ships;

/// <summary>Ship use cases: create (US-03), list (US-04), read, rename / (de)activate.</summary>
public sealed partial class ShipService(
    IShipRepository repository,
    ICurrentUser currentUser,
    IValidator<CreateShipRequest> createValidator,
    IValidator<UpdateShipRequest> updateValidator,
    IValidator<ListShipsQuery> listValidator,
    ILogger<ShipService> logger)
{
    public async Task<ShipDto> CreateAsync(CreateShipRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);

        var ship = await repository.CreateAsync(
            ValidationExtensions.ParseShipCode(request.ShipCode),
            request.ShipName!.Trim(),
            request.FiscalYearCode!.Trim(),
            string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim(),
            currentUser.UserId,
            cancellationToken);

        LogShipCreated(logger, ship.ShipCode, ship.FiscalYearCode, currentUser.UserId);
        return ship;
    }

    public Task<ShipDto> GetAsync(string shipCode, CancellationToken cancellationToken)
    {
        var code = ValidationExtensions.ParseShipCode(shipCode);
        EnsureAccess(code);
        return repository.GetByCodeAsync(code, currentUser.UserId, cancellationToken);
    }

    public async Task<PagedResult<ShipDto>> ListAsync(ListShipsQuery query, CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken);

        return await repository.ListAsync(
            query.PageNumber ?? PageRequest.DefaultPageNumber,
            query.PageSize ?? PageRequest.DefaultPageSize,
            string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim(),
            currentUser.UserId,
            cancellationToken);
    }

    /// <summary>
    /// Renames and/or (de)activates a ship. With <paramref name="expectedVersion"/> (If-Match) the update is
    /// rejected with 412 when someone else changed the ship first (optimistic concurrency, D-30).
    /// </summary>
    public async Task<ShipDto> UpdateAsync(
        string shipCode, UpdateShipRequest request, RowVersion? expectedVersion, CancellationToken cancellationToken)
    {
        var code = ValidationExtensions.ParseShipCode(shipCode);
        await updateValidator.EnsureValidAsync(request, cancellationToken);

        var ship = await repository.UpdateAsync(
            code,
            request.ShipName?.Trim(),
            string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim(),
            expectedVersion,
            currentUser.UserId,
            cancellationToken);

        LogShipUpdated(logger, ship.ShipCode, ship.Status, currentUser.UserId);
        return ship;
    }

    /// <summary>Unassigned ships are reported as not found, never as forbidden (D-04: no enumeration).</summary>
    private void EnsureAccess(string shipCode)
    {
        if (!currentUser.CanAccessShip(shipCode))
        {
            throw new NotFoundException(ErrorCodes.ShipNotFound, $"Ship '{shipCode}' was not found.");
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Ship {ShipCode} created with fiscal year {FiscalYearCode} by user {ActorUserId}")]
    private static partial void LogShipCreated(ILogger logger, string shipCode, string fiscalYearCode, int actorUserId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "Ship {ShipCode} updated (status {Status}) by user {ActorUserId}")]
    private static partial void LogShipUpdated(ILogger logger, string shipCode, string status, int actorUserId);
}
