using System.Data;
using Dapper;
using ShipManagement.Application.Common;
using ShipManagement.Application.Crew;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence.Repositories;

internal sealed class CrewRepository(StoredProcedureExecutor executor) : ICrewRepository
{
    public async Task<PagedResult<CrewMemberDto>> ListByShipAsync(
        string shipCode, CrewListCriteria criteria, int requestedByUserId, CancellationToken cancellationToken)
    {
        // @AsOfDate is intentionally not passed: "today" is the UTC date on the server (D-02).
        var parameters = new DynamicParameters()
            .AddAnsi("@ShipCode", shipCode, 20)
            .AddInt("@RequestedByUserId", requestedByUserId)
            .AddInt("@PageNumber", criteria.PageNumber)
            .AddInt("@PageSize", criteria.PageSize)
            .AddAnsi("@SortBy", criteria.SortBy, 30)
            .AddAnsi("@SortDirection", criteria.SortDirection, 10)
            .AddUnicode("@Search", criteria.Search, 200)
            .AddOutput("@TotalCount", DbType.Int32);

        var rows = await executor.QueryAsync<CrewRow>("app.usp_Crew_ListByShip", parameters, CommandKind.Read, cancellationToken);

        var items = rows
            .Select(r => new CrewMemberDto(
                r.RankName, r.CrewMemberId, r.FirstName, r.LastName, r.Age, r.Nationality,
                DateOnly.FromDateTime(r.SignOnDate), r.SignOnDateLabel, r.Status))
            .ToList();

        return new PagedResult<CrewMemberDto>(items, criteria.PageNumber, criteria.PageSize, parameters.Get<int>("@TotalCount"));
    }
}
