using Microsoft.Extensions.Logging;
using ShipManagement.Application.Common;
using ShipManagement.Application.Security;
using ShipManagement.Domain.Errors;

namespace ShipManagement.Application.Assignments;

/// <summary>A ship assigned to a user, with its status (US-06).</summary>
public sealed record AssignedShipDto(string ShipCode, string ShipName, string FiscalYearCode, string Status, DateTime AssignedAtUtc);

/// <summary>User-ship assignment data access; every method maps to one stored procedure.</summary>
public interface IUserShipRepository
{
    /// <summary>Returns true when a new assignment was created, false when it already existed.</summary>
    Task<bool> AssignAsync(int userId, string shipCode, int requestedByUserId, CancellationToken cancellationToken);

    /// <summary>Returns true when an assignment was removed, false when there was none.</summary>
    Task<bool> UnassignAsync(int userId, string shipCode, int requestedByUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AssignedShipDto>> ListByUserAsync(int userId, int requestedByUserId, CancellationToken cancellationToken);
}

/// <summary>Assign / unassign ships (US-05) and list a user's ships (US-06).</summary>
public sealed partial class AssignmentService(
    IUserShipRepository repository,
    ICurrentUser currentUser,
    IAuthContextInvalidator authContextInvalidator,
    ILogger<AssignmentService> logger)
{
    /// <summary>Idempotent assign. Returns true when the assignment is new.</summary>
    public async Task<bool> AssignAsync(int userId, string shipCode, CancellationToken cancellationToken)
    {
        ValidationExtensions.EnsurePositiveId(userId, "userId");
        var code = ValidationExtensions.ParseShipCode(shipCode);

        var created = await repository.AssignAsync(userId, code, currentUser.UserId, cancellationToken);
        if (created)
        {
            // The user's cached ship list is stale now; their next request re-reads it (D-25).
            authContextInvalidator.Invalidate(userId);
            LogShipAssigned(logger, code, userId, currentUser.UserId);
        }

        return created;
    }

    /// <summary>Idempotent unassign.</summary>
    public async Task UnassignAsync(int userId, string shipCode, CancellationToken cancellationToken)
    {
        ValidationExtensions.EnsurePositiveId(userId, "userId");
        var code = ValidationExtensions.ParseShipCode(shipCode);

        var removed = await repository.UnassignAsync(userId, code, currentUser.UserId, cancellationToken);
        if (removed)
        {
            authContextInvalidator.Invalidate(userId);
            LogShipUnassigned(logger, code, userId, currentUser.UserId);
        }
    }

    /// <summary>Ships of a user: the caller themselves, or anyone for an administrator (SEC-04).</summary>
    public Task<IReadOnlyList<AssignedShipDto>> ListByUserAsync(int userId, CancellationToken cancellationToken)
    {
        ValidationExtensions.EnsurePositiveId(userId, "userId");
        if (userId != currentUser.UserId && !currentUser.IsAdministrator)
        {
            throw new ForbiddenException("You are not allowed to view this user's ships.");
        }

        return repository.ListByUserAsync(userId, currentUser.UserId, cancellationToken);
    }

    public Task<IReadOnlyList<AssignedShipDto>> ListMineAsync(CancellationToken cancellationToken) =>
        repository.ListByUserAsync(currentUser.UserId, currentUser.UserId, cancellationToken);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "Ship {ShipCode} assigned to user {UserId} by user {ActorUserId}")]
    private static partial void LogShipAssigned(ILogger logger, string shipCode, int userId, int actorUserId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "Ship {ShipCode} unassigned from user {UserId} by user {ActorUserId}")]
    private static partial void LogShipUnassigned(ILogger logger, string shipCode, int userId, int actorUserId);
}
