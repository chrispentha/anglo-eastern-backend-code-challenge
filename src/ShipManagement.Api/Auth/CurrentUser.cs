using System.Globalization;
using ShipManagement.Application.Common;

namespace ShipManagement.Api.Auth;

/// <summary>Reads the caller from the authenticated principal of the current request.</summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public int UserId =>
        int.Parse(
            User.FindFirst(AppClaimTypes.UserId)?.Value
                ?? throw new InvalidOperationException("The request is not authenticated."),
            CultureInfo.InvariantCulture);

    public bool IsAdministrator => User.HasClaim(AppClaimTypes.IsAdministrator, "true");

    public bool CanAccessShip(string shipCode) =>
        IsAdministrator
        || User.FindAll(AppClaimTypes.Ship).Any(c => string.Equals(c.Value, shipCode, StringComparison.OrdinalIgnoreCase));

    private System.Security.Claims.ClaimsPrincipal User =>
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("No active HTTP request.");
}
