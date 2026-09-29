using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipManagement.Api.Auth;
using ShipManagement.Application.Assignments;

namespace ShipManagement.Api.Controllers;

/// <summary>Ship assignments (US-05) and "ships by user" (US-06).</summary>
[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
public sealed class UserShipsController(AssignmentService assignments) : ControllerBase
{
    /// <summary>Ships assigned to the calling user, with status.</summary>
    [HttpGet("me/ships")]
    [ProducesResponseType<IReadOnlyList<AssignedShipDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<IReadOnlyList<AssignedShipDto>> ListMine(CancellationToken cancellationToken) =>
        assignments.ListMineAsync(cancellationToken);

    /// <summary>Ships assigned to a user. Allowed for the user themselves and for administrators.</summary>
    [HttpGet("{userId:int}/ships")]
    [ProducesResponseType<IReadOnlyList<AssignedShipDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<AssignedShipDto>> ListByUser(int userId, CancellationToken cancellationToken) =>
        assignments.ListByUserAsync(userId, cancellationToken);

    /// <summary>Assigns a ship to a user. Idempotent. Administrator only.</summary>
    /// <response code="201">A new assignment was created.</response>
    /// <response code="204">The ship was already assigned; nothing changed.</response>
    /// <response code="404">Unknown user or ship.</response>
    [HttpPut("{userId:int}/ships/{shipCode}")]
    [Authorize(Policy = AuthPolicies.Administrator)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(int userId, string shipCode, CancellationToken cancellationToken)
    {
        var created = await assignments.AssignAsync(userId, shipCode, cancellationToken);
        return created
            ? Created(Url.Action(nameof(ListByUser), new { userId }), null)
            : NoContent();
    }

    /// <summary>Removes a ship assignment. Idempotent. Administrator only.</summary>
    /// <response code="204">The ship is no longer assigned to the user.</response>
    /// <response code="404">Unknown user or ship.</response>
    [HttpDelete("{userId:int}/ships/{shipCode}")]
    [Authorize(Policy = AuthPolicies.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unassign(int userId, string shipCode, CancellationToken cancellationToken)
    {
        await assignments.UnassignAsync(userId, shipCode, cancellationToken);
        return NoContent();
    }
}
