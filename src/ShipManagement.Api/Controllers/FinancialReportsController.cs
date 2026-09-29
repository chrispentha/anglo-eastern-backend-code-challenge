using Microsoft.AspNetCore.Mvc;
using ShipManagement.Application.FinancialReports;

namespace ShipManagement.Api.Controllers;

/// <summary>Financial expense reports per ship and accounting period (US-08, US-09).</summary>
[ApiController]
[Route("api/v1/ships/{shipCode}/financial-reports")]
[Produces("application/json")]
public sealed class FinancialReportsController(FinancialReportService reports) : ControllerBase
{
    /// <summary>
    /// Detail report: every account with data, parents and children in tree order, with actual, budget and variance
    /// for the period and for the ship's fiscal year to date. Amounts are null when there is no data.
    /// </summary>
    /// <param name="shipCode">Ship code, e.g. SHIP02.</param>
    /// <param name="query">period: accounting period yyyy-MM, e.g. 2025-02.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("detail")]
    [ProducesResponseType<FinancialReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<FinancialReportDto> Detail(string shipCode, [FromQuery] FinancialReportQuery query, CancellationToken cancellationToken) =>
        reports.GetAsync(shipCode, FinancialReportType.Detail, query, cancellationToken);

    /// <summary>Summary report: parent (summary) accounts only plus the grand total; ties to the detail report.</summary>
    /// <param name="shipCode">Ship code, e.g. SHIP02.</param>
    /// <param name="query">period: accounting period yyyy-MM, e.g. 2025-02.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("summary")]
    [ProducesResponseType<FinancialReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<FinancialReportDto> Summary(string shipCode, [FromQuery] FinancialReportQuery query, CancellationToken cancellationToken) =>
        reports.GetAsync(shipCode, FinancialReportType.Summary, query, cancellationToken);
}
