using System.Data;
using Dapper;
using ShipManagement.Application.Security;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence.Repositories;

/// <summary>Reads caller security contexts (user, role flags, assigned ships).</summary>
internal sealed class AuthRepository(StoredProcedureExecutor executor)
{
    public Task<AuthContext?> ResolveApiKeyAsync(byte[] keyHash, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@KeyHash", keyHash, DbType.Binary, size: 32);
        return executor.QueryMultipleAsync("app.usp_Auth_ResolveApiKey", parameters, ReadAsync, CommandKind.Read, cancellationToken);
    }

    public Task<AuthContext?> ResolveUserAsync(int userId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters().AddInt("@UserId", userId);
        return executor.QueryMultipleAsync("app.usp_Auth_ResolveUser", parameters, ReadAsync, CommandKind.Read, cancellationToken);
    }

    private static async Task<AuthContext?> ReadAsync(SqlMapper.GridReader grid)
    {
        var user = await grid.ReadSingleOrDefaultAsync<AuthUserRow>();
        var shipCodes = (await grid.ReadAsync<string>()).AsList();
        return user is null ? null : new AuthContext(user.UserId, user.RoleName, user.IsAdministrator, shipCodes);
    }
}
