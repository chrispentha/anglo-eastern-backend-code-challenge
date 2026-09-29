using System.Data;
using Dapper;
using ShipManagement.Application.Common;
using ShipManagement.Application.Ships;
using ShipManagement.Domain.ValueObjects;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence.Repositories;

internal sealed class ShipRepository(StoredProcedureExecutor executor) : IShipRepository
{
    public async Task<ShipDto> CreateAsync(
        string shipCode, string shipName, string fiscalYearCode, string? status, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddAnsi("@ShipCode", shipCode, 20)
            .AddUnicode("@ShipName", shipName, 200)
            .AddAnsi("@FiscalYearCode", fiscalYearCode, 10)
            .AddAnsi("@StatusName", status, 30)
            .AddInt("@RequestedByUserId", requestedByUserId);

        return ToDto(await executor.QuerySingleAsync<ShipRow>("app.usp_Ship_Create", parameters, CommandKind.Write, cancellationToken));
    }

    public async Task<ShipDto> GetByCodeAsync(string shipCode, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddAnsi("@ShipCode", shipCode, 20)
            .AddInt("@RequestedByUserId", requestedByUserId);

        return ToDto(await executor.QuerySingleAsync<ShipRow>("app.usp_Ship_GetByCode", parameters, CommandKind.Read, cancellationToken));
    }

    public async Task<PagedResult<ShipDto>> ListAsync(
        int pageNumber, int pageSize, string? status, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddInt("@RequestedByUserId", requestedByUserId)
            .AddInt("@PageNumber", pageNumber)
            .AddInt("@PageSize", pageSize)
            .AddAnsi("@StatusName", status, 30)
            .AddOutput("@TotalCount", DbType.Int32);

        var rows = await executor.QueryAsync<ShipRow>("app.usp_Ship_List", parameters, CommandKind.Read, cancellationToken);
        return new PagedResult<ShipDto>(
            rows.Select(ToDto).ToList(), pageNumber, pageSize, parameters.Get<int>("@TotalCount"));
    }

    public async Task<ShipDto> UpdateAsync(
        string shipCode, string? shipName, string? status, RowVersion? expectedVersion, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddAnsi("@ShipCode", shipCode, 20)
            .AddUnicode("@ShipName", shipName, 200)
            .AddAnsi("@StatusName", status, 30)
            .AddInt("@RequestedByUserId", requestedByUserId);
        parameters.Add("@ExpectedRowVersion", expectedVersion?.ToBytes(), DbType.Binary, size: RowVersion.ByteLength);

        return ToDto(await executor.QuerySingleAsync<ShipRow>("app.usp_Ship_Update", parameters, CommandKind.Write, cancellationToken));
    }

    private static ShipDto ToDto(ShipRow row) =>
        new(row.ShipCode, row.ShipName, row.FiscalYearCode, row.Status,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc), DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc),
            RowVersion.FromBytes(row.RowVersion).ToString());
}
