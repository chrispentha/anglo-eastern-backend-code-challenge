using System.Data;
using Dapper;
using ShipManagement.Application.FinancialReports;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence.Repositories;

internal sealed class FinancialReportRepository(StoredProcedureExecutor executor) : IFinancialReportRepository
{
    public async Task<FinancialReportData> GetAsync(
        string shipCode, DateOnly period, FinancialReportType reportType, int requestedByUserId, CancellationToken cancellationToken)
    {
        var procedure = reportType == FinancialReportType.Summary
            ? "app.usp_FinancialReport_Summary"
            : "app.usp_FinancialReport_Detail";

        var parameters = new DynamicParameters()
            .AddAnsi("@ShipCode", shipCode, 20)
            .AddDate("@Period", period)
            .AddInt("@RequestedByUserId", requestedByUserId)
            .AddOutput("@YtdStart", DbType.Date)
            .AddOutput("@YtdEnd", DbType.Date)
            .AddOutput("@FiscalYearCode", DbType.AnsiStringFixedLength, 4);

        var rows = await executor.QueryAsync<FinancialLineRow>(procedure, parameters, CommandKind.Read, cancellationToken);

        var lines = rows
            .Select(r => new FinancialReportLineDto(
                r.AccountNumber, r.Description, r.AccountType, r.HierarchyLevel, r.ParentAccountNumber,
                r.Actual, r.Budget, r.Variance, r.ActualYtd, r.BudgetYtd, r.VarianceYtd, r.IsTotal))
            .ToList();

        return new FinancialReportData(
            parameters.Get<string>("@FiscalYearCode"),
            DateOnly.FromDateTime(parameters.Get<DateTime>("@YtdStart")),
            DateOnly.FromDateTime(parameters.Get<DateTime>("@YtdEnd")),
            lines);
    }
}
