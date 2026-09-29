using FluentValidation;
using Microsoft.Extensions.Logging;
using ShipManagement.Application.Common;
using ShipManagement.Domain.Errors;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Application.FinancialReports;

/// <summary>Detail: every account (parents and children). Summary: parent (summary) accounts only (D-09).</summary>
public enum FinancialReportType
{
    Detail,
    Summary,
}

/// <summary>
/// One report line. Amounts are null when there is no data and 0 when the data sums to zero (D-06);
/// variances are Actual - Budget. The last line is the grand total (<see cref="IsTotal"/>).
/// </summary>
public sealed record FinancialReportLineDto(
    string? AccountNumber,
    string Description,
    string? AccountType,
    int Level,
    string? ParentAccountNumber,
    decimal? Actual,
    decimal? Budget,
    decimal? Variance,
    decimal? ActualYtd,
    decimal? BudgetYtd,
    decimal? VarianceYtd,
    bool IsTotal);

/// <summary>Ready-made column headings, e.g. "Actual YTD (Apr 2024 - Feb 2025)".</summary>
public sealed record FinancialReportColumnLabels(
    string Actual, string Budget, string Variance, string ActualYtd, string BudgetYtd, string VarianceYtd);

/// <summary>Financial expense report for one ship and one accounting period.</summary>
public sealed record FinancialReportDto(
    string ShipCode,
    string ReportType,
    string FiscalYearCode,
    string Period,
    string YtdStart,
    string YtdEnd,
    FinancialReportColumnLabels Columns,
    IReadOnlyList<FinancialReportLineDto> Lines);

/// <summary>Query string for a financial report.</summary>
public sealed class FinancialReportQuery
{
    /// <summary>Accounting period, yyyy-MM (e.g. 2025-07).</summary>
    public string? Period { get; init; }
}

/// <summary>What the report stored procedures return.</summary>
public sealed record FinancialReportData(
    string FiscalYearCode, DateOnly YtdStart, DateOnly YtdEnd, IReadOnlyList<FinancialReportLineDto> Lines);

public interface IFinancialReportRepository
{
    Task<FinancialReportData> GetAsync(
        string shipCode, DateOnly period, FinancialReportType reportType, int requestedByUserId, CancellationToken cancellationToken);
}

public sealed class FinancialReportQueryValidator : AbstractValidator<FinancialReportQuery>
{
    public FinancialReportQueryValidator()
    {
        RuleFor(q => q.Period)
            .Must(p => AccountingPeriod.TryParse(p?.Trim(), out _))
            .WithMessage(ValidationMessages.Period);
    }
}

/// <summary>Financial expense reports per ship and accounting period (US-08, US-09).</summary>
public sealed partial class FinancialReportService(
    IFinancialReportRepository repository,
    ICurrentUser currentUser,
    IValidator<FinancialReportQuery> validator,
    ILogger<FinancialReportService> logger)
{
    public async Task<FinancialReportDto> GetAsync(
        string shipCode, FinancialReportType reportType, FinancialReportQuery query, CancellationToken cancellationToken)
    {
        var code = ValidationExtensions.ParseShipCode(shipCode);
        await validator.EnsureValidAsync(query, cancellationToken);
        var period = AccountingPeriod.Parse(query.Period!.Trim());

        if (!currentUser.CanAccessShip(code))
        {
            throw new NotFoundException(ErrorCodes.ShipNotFound, $"Ship '{code}' was not found.");
        }

        var data = await repository.GetAsync(code, period.FirstDay, reportType, currentUser.UserId, cancellationToken);

        var ytdStart = AccountingPeriod.FromDate(data.YtdStart);
        var ytdEnd = AccountingPeriod.FromDate(data.YtdEnd);
        var periodLabel = period.ToLabel();
        var ytdLabel = $"{ytdStart.ToLabel()} - {ytdEnd.ToLabel()}";

        LogReportRequested(logger, reportType, code, period.ToString(), currentUser.UserId);

        return new FinancialReportDto(
            code,
            reportType.ToString(),
            data.FiscalYearCode,
            period.ToString(),
            ytdStart.ToString(),
            ytdEnd.ToString(),
            new FinancialReportColumnLabels(
                $"Actual ({periodLabel})",
                $"Budget ({periodLabel})",
                $"Variance ({periodLabel})",
                $"Actual YTD ({ytdLabel})",
                $"Budget YTD ({ytdLabel})",
                $"Variance YTD ({ytdLabel})"),
            data.Lines);
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "{ReportType} financial report for ship {ShipCode}, period {Period} requested by user {ActorUserId}")]
    private static partial void LogReportRequested(
        ILogger logger, FinancialReportType reportType, string shipCode, string period, int actorUserId);
}
