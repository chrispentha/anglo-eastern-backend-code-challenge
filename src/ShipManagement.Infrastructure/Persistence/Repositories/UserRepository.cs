using System.Data;
using Dapper;
using ShipManagement.Application.Common;
using ShipManagement.Application.Users;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(StoredProcedureExecutor executor) : IUserRepository
{
    public async Task<UserDto> CreateAsync(
        string fullName, string? email, string role, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddUnicode("@FullName", fullName, 200)
            .AddUnicode("@Email", email, 300)
            .AddAnsi("@RoleName", role, 50)
            .AddInt("@RequestedByUserId", requestedByUserId);

        var row = await executor.QuerySingleAsync<UserRow>("app.usp_User_Create", parameters, CommandKind.Write, cancellationToken);
        return ToDto(row);
    }

    public async Task<UserDto> GetByIdAsync(int userId, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddInt("@UserId", userId)
            .AddInt("@RequestedByUserId", requestedByUserId);

        var row = await executor.QuerySingleAsync<UserRow>("app.usp_User_GetById", parameters, CommandKind.Read, cancellationToken);
        return ToDto(row);
    }

    public async Task<PagedResult<UserDto>> ListAsync(
        int pageNumber, int pageSize, string? role, string sortDirection, int requestedByUserId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters()
            .AddInt("@RequestedByUserId", requestedByUserId)
            .AddInt("@PageNumber", pageNumber)
            .AddInt("@PageSize", pageSize)
            .AddAnsi("@RoleName", role, 50)
            .AddAnsi("@SortDirection", sortDirection, 10)
            .AddOutput("@TotalCount", DbType.Int32);

        var rows = await executor.QueryAsync<UserRow>("app.usp_User_List", parameters, CommandKind.Read, cancellationToken);
        return new PagedResult<UserDto>(
            rows.Select(ToDto).ToList(), pageNumber, pageSize, parameters.Get<int>("@TotalCount"));
    }

    private static UserDto ToDto(UserRow row) =>
        new(row.UserId, row.FullName, row.Email, row.RoleName, row.IsActive, DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));
}
