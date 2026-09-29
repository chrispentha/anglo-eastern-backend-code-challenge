using System.Data;
using Dapper;
using ShipManagement.Application.Assignments;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence.Repositories;

internal sealed class UserShipRepository(StoredProcedureExecutor executor) : IUserShipRepository
{
    public async Task<bool> AssignAsync(int userId, string shipCode, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = Parameters(userId, shipCode, requestedByUserId).AddOutput("@Created", DbType.Boolean);
        await executor.ExecuteAsync("app.usp_UserShip_Assign", parameters, CommandKind.IdempotentWrite, cancellationToken);
        return parameters.Get<bool>("@Created");
    }

    public async Task<bool> UnassignAsync(int userId, string shipCode, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = Parameters(userId, shipCode, requestedByUserId).AddOutput("@Removed", DbType.Boolean);
        await executor.ExecuteAsync("app.usp_UserShip_Unassign", parameters, CommandKind.IdempotentWrite, cancellationToken);
        return parameters.Get<bool>("@Removed");
    }

    public async Task<IReadOnlyList<AssignedShipDto>> ListByUserAsync(int userId, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddInt("@UserId", userId)
            .AddInt("@RequestedByUserId", requestedByUserId);

        var rows = await executor.QueryAsync<AssignedShipRow>("app.usp_UserShip_ListByUser", parameters, CommandKind.Read, cancellationToken);
        return rows
            .Select(r => new AssignedShipDto(r.ShipCode, r.ShipName, r.FiscalYearCode, r.Status,
                DateTime.SpecifyKind(r.AssignedAtUtc, DateTimeKind.Utc)))
            .ToList();
    }

    private static DynamicParameters Parameters(int userId, string shipCode, int requestedByUserId) =>
        new DynamicParameters()
            .AddInt("@UserId", userId)
            .AddAnsi("@ShipCode", shipCode, 20)
            .AddInt("@RequestedByUserId", requestedByUserId);
}
