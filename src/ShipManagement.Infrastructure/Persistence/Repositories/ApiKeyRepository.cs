using System.Data;
using Dapper;
using ShipManagement.Application.ApiKeys;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence.Repositories;

internal sealed class ApiKeyRepository(StoredProcedureExecutor executor) : IApiKeyRepository
{
    public async Task<ApiKeyDto> CreateAsync(
        int userId, string keyPrefix, byte[] keyHash, DateTime expiresAtUtc, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddInt("@UserId", userId)
            .AddAnsi("@KeyPrefix", keyPrefix, 20)
            .AddInt("@RequestedByUserId", requestedByUserId);
        parameters.Add("@KeyHash", keyHash, DbType.Binary, size: 64);
        parameters.Add("@ExpiresAtUtc", expiresAtUtc, DbType.DateTime2);

        return ToDto(await executor.QuerySingleAsync<ApiKeyRow>("app.usp_ApiKey_Create", parameters, CommandKind.Write, cancellationToken));
    }

    public async Task<IReadOnlyList<ApiKeyDto>> ListByUserAsync(int userId, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddInt("@UserId", userId)
            .AddInt("@RequestedByUserId", requestedByUserId);

        var rows = await executor.QueryAsync<ApiKeyRow>("app.usp_ApiKey_ListByUser", parameters, CommandKind.Read, cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public Task RevokeAsync(int userId, int apiKeyId, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddInt("@UserId", userId)
            .AddInt("@ApiKeyId", apiKeyId)
            .AddInt("@RequestedByUserId", requestedByUserId);

        return executor.ExecuteAsync("app.usp_ApiKey_Revoke", parameters, CommandKind.IdempotentWrite, cancellationToken);
    }

    private static ApiKeyDto ToDto(ApiKeyRow row) =>
        new(row.ApiKeyId, row.UserId, row.KeyPrefix,
            AsUtc(row.CreatedAtUtc), AsUtc(row.ExpiresAtUtc), AsUtc(row.RevokedAtUtc), AsUtc(row.LastUsedAtUtc));

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value) => value is null ? null : AsUtc(value.Value);
}
