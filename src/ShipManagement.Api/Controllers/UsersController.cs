using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipManagement.Api.Auth;
using ShipManagement.Application.Common;
using ShipManagement.Application.Users;

namespace ShipManagement.Api.Controllers;

/// <summary>Application users (US-01, US-02).</summary>
[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
public sealed class UsersController(UserService users) : ControllerBase
{
    /// <summary>Creates a user. Administrator only.</summary>
    /// <response code="201">The user was created; the Location header points to it.</response>
    /// <response code="400">Validation failed, e.g. unknown role or invalid e-mail.</response>
    /// <response code="409">A user with the same e-mail already exists.</response>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.Administrator)]
    [Consumes("application/json")]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { userId = user.UserId }, user);
    }

    /// <summary>Lists users, sorted by name, optionally filtered by role. Administrator only.</summary>
    [HttpGet]
    [Authorize(Policy = AuthPolicies.Administrator)]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<PagedResult<UserDto>> List([FromQuery] ListUsersQuery query, CancellationToken cancellationToken) =>
        users.ListAsync(query, cancellationToken);

    /// <summary>Returns one user. Administrator only.</summary>
    [HttpGet("{userId:int}")]
    [Authorize(Policy = AuthPolicies.Administrator)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<UserDto> GetById(int userId, CancellationToken cancellationToken) =>
        users.GetByIdAsync(userId, cancellationToken);

    /// <summary>Returns the calling user.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<UserDto> Me(CancellationToken cancellationToken) => users.GetCurrentAsync(cancellationToken);
}
