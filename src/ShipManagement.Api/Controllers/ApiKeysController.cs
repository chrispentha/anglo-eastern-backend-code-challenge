using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ShipManagement.Api.Auth;
using ShipManagement.Application.ApiKeys;

namespace ShipManagement.Api.Controllers;

/// <summary>API keys of a user (SEC-03). Administrator only.</summary>
[ApiController]
[Route("api/v1/users/{userId:int}/api-keys")]
[Authorize(Policy = AuthPolicies.Administrator)]
[Produces("application/json")]
public sealed class ApiKeysController(ApiKeyService apiKeys) : ControllerBase
{
    /// <summary>Issues a new API key. The key is returned once in this response and cannot be retrieved again.</summary>
    /// <response code="201">The key was issued.</response>
    /// <response code="404">Unknown or inactive user.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CreatedApiKeyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreatedApiKeyDto>> Create(
        int userId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateApiKeyRequest? request,
        CancellationToken cancellationToken)
    {
        var created = await apiKeys.CreateAsync(userId, request ?? new CreateApiKeyRequest(), cancellationToken);
        return CreatedAtAction(nameof(List), new { userId }, created);
    }

    /// <summary>Lists a user's API keys (metadata only, never the key or its hash).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ApiKeyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<ApiKeyDto>> List(int userId, CancellationToken cancellationToken) =>
        apiKeys.ListAsync(userId, cancellationToken);

    /// <summary>Revokes an API key. Idempotent; takes effect immediately.</summary>
    [HttpDelete("{apiKeyId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(int userId, int apiKeyId, CancellationToken cancellationToken)
    {
        await apiKeys.RevokeAsync(userId, apiKeyId, cancellationToken);
        return NoContent();
    }
}
