using Microsoft.AspNetCore.Mvc;
using ShipManagement.Application.Common;
using ShipManagement.Application.Crew;

namespace ShipManagement.Api.Controllers;

/// <summary>Crew lists (US-07).</summary>
[ApiController]
[Route("api/v1/ships/{shipCode}/crew")]
[Produces("application/json")]
public sealed class CrewController(CrewService crew) : ControllerBase
{
    /// <summary>
    /// Crew currently Onboard or Relief Due on an active ship, as of today (UTC).
    /// Paged, sortable by any column (rank sorts by seniority) and searchable on every column except status,
    /// including partial dates such as "05 Apr".
    /// </summary>
    /// <response code="200">A page of crew members.</response>
    /// <response code="400">Invalid paging, sort or search parameters.</response>
    /// <response code="404">The ship does not exist or is not assigned to you.</response>
    /// <response code="409">The ship is inactive (code SHIP_INACTIVE).</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<CrewMemberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<PagedResult<CrewMemberDto>> List(string shipCode, [FromQuery] CrewListQuery query, CancellationToken cancellationToken) =>
        crew.ListAsync(shipCode, query, cancellationToken);
}
