using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ShipManagement.IntegrationTests.Infrastructure;

/// <summary>Calls stored procedures exactly as the API does: as the least-privilege login, typed parameters only.</summary>
internal sealed class ProcedureClient(string connectionString)
{
    public async Task<(List<CrewRow> Rows, int TotalCount)> CrewListAsync(
        string shipCode, int requestedBy, int pageNumber = 1, int pageSize = 20, string sortBy = "rank",
        string sortDirection = "asc", string? search = null, DateTime? asOfDate = null)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ShipCode", shipCode, DbType.AnsiString, size: 20);
        parameters.Add("@RequestedByUserId", requestedBy, DbType.Int32);
        parameters.Add("@PageNumber", pageNumber, DbType.Int32);
        parameters.Add("@PageSize", pageSize, DbType.Int32);
        parameters.Add("@SortBy", sortBy, DbType.AnsiString, size: 30);
        parameters.Add("@SortDirection", sortDirection, DbType.AnsiString, size: 10);
        parameters.Add("@Search", search, DbType.String, size: 200);
        parameters.Add("@AsOfDate", asOfDate ?? TestData.AsOfDate, DbType.Date);
        parameters.Add("@TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await using var connection = new SqlConnection(connectionString);
        var rows = (await connection.QueryAsync<CrewRow>(
            "app.usp_Crew_ListByShip", parameters, commandType: CommandType.StoredProcedure)).AsList();
        return (rows, parameters.Get<int>("@TotalCount"));
    }

    public async Task<(List<ReportRow> Rows, DateTime YtdStart, DateTime YtdEnd, string FiscalYearCode)> ReportAsync(
        string procedure, string shipCode, DateTime period, int requestedBy)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ShipCode", shipCode, DbType.AnsiString, size: 20);
        parameters.Add("@Period", period, DbType.Date);
        parameters.Add("@RequestedByUserId", requestedBy, DbType.Int32);
        parameters.Add("@YtdStart", dbType: DbType.Date, direction: ParameterDirection.Output);
        parameters.Add("@YtdEnd", dbType: DbType.Date, direction: ParameterDirection.Output);
        parameters.Add("@FiscalYearCode", dbType: DbType.AnsiStringFixedLength, size: 4, direction: ParameterDirection.Output);

        await using var connection = new SqlConnection(connectionString);
        var rows = (await connection.QueryAsync<ReportRow>(procedure, parameters, commandType: CommandType.StoredProcedure)).AsList();
        return (rows, parameters.Get<DateTime>("@YtdStart"), parameters.Get<DateTime>("@YtdEnd"), parameters.Get<string>("@FiscalYearCode"));
    }

    public async Task ExecuteAsync(string procedure, object parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(procedure, parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task ExecuteTextAsync(string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(sql);
    }
}

internal sealed class CrewRow
{
    public string RankName { get; set; } = string.Empty;
    public string CrewMemberId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Nationality { get; set; } = string.Empty;
    public DateTime SignOnDate { get; set; }
    public string SignOnDateLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

internal sealed class ReportRow
{
    public string? AccountNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? AccountType { get; set; }
    public int HierarchyLevel { get; set; }
    public string? ParentAccountNumber { get; set; }
    public decimal? Actual { get; set; }
    public decimal? Budget { get; set; }
    public decimal? Variance { get; set; }
    public decimal? ActualYtd { get; set; }
    public decimal? BudgetYtd { get; set; }
    public decimal? VarianceYtd { get; set; }
    public bool IsTotal { get; set; }
}
