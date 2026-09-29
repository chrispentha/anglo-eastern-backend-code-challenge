using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipManagement.Api.Auth;
using ShipManagement.Api.Http;
using ShipManagement.Application.Common;
using ShipManagement.Application.Ships;

namespace ShipManagement.Api.Controllers;

/// <summary>Ships (US-03, US-04).</summary>
[ApiController]
[Route("api/v1/ships")]
[Produces("application/json")]
public sealed class ShipsController(ShipService ships) : ControllerBase
{
    /// <summary>Creates a ship. Administrator only.</summary>
    /// <response code="201">The ship was created; the Location header points to it.</response>
    /// <response code="400">Validation failed, e.g. invalid ship code or unknown fiscal year code.</response>
    /// <response code="409">The ship code already exists.</response>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.Administrator)]
    [Consumes("application/json")]
    [ProducesResponseType<ShipDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShipDto>> Create(CreateShipRequest request, CancellationToken cancellationToken)
    {
        var ship = await ships.CreateAsync(request, cancellationToken);
        Response.Headers.ETag = EntityTags.Format(ship.Version);
        return CreatedAtAction(nameof(GetByCode), new { shipCode = ship.ShipCode }, ship);
    }

    /// <summary>Lists ships: every ship for administrators, assigned ships for everyone else.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ShipDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<PagedResult<ShipDto>> List([FromQuery] ListShipsQuery query, CancellationToken cancellationToken) =>
        ships.ListAsync(query, cancellationToken);

    /// <summary>Returns one ship, with its version in the ETag header. Unassigned ships are reported as not found.</summary>
    [HttpGet("{shipCode}")]
    [ProducesResponseType<ShipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ShipDto> GetByCode(string shipCode, CancellationToken cancellationToken)
    {
        var ship = await ships.GetAsync(shipCode, cancellationToken);
        Response.Headers.ETag = EntityTags.Format(ship.Version);
        return ship;
    }

    /// <summary>
    /// Renames a ship and/or changes its status (e.g. deactivation). Administrator only.
    /// Send the ETag from a previous read in If-Match to make sure nobody changed the ship in the meantime:
    /// a stale ETag is rejected with 412 instead of silently overwriting the other change.
    /// </summary>
    /// <param name="shipCode">Ship code.</param>
    /// <param name="request">Fields to change.</param>
    /// <param name="ifMatch">Optional ETag from a previous read, e.g. "00000000000007D3".</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The ship was updated; the new version is in the ETag header.</response>
    /// <response code="412">The ship changed since it was read (code PRECONDITION_FAILED).</response>
    [HttpPatch("{shipCode}")]
    [Authorize(Policy = AuthPolicies.Administrator)]
    [Consumes("application/json")]
    [ProducesResponseType<ShipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    public async Task<ShipDto> Update(
        string shipCode,
        UpdateShipRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        var ship = await ships.UpdateAsync(shipCode, request, EntityTags.ParseIfMatch(ifMatch), cancellationToken);
        Response.Headers.ETag = EntityTags.Format(ship.Version);
        return ship;
    }
}
