using System.Globalization;
using System.Security.Claims;
using ShipManagement.Application.Security;

namespace ShipManagement.Api.Auth;

/// <summary>Builds the request principal from the database-resolved context, whatever the credential type.</summary>
internal static class PrincipalFactory
{
    public static ClaimsPrincipal Create(AuthContext context, string authenticationType)
    {
        var claims = new List<Claim>(3 + context.ShipCodes.Count)
        {
            new(AppClaimTypes.UserId, context.UserId.ToString(CultureInfo.InvariantCulture)),
            new(AppClaimTypes.Role, context.Role),
            new(AppClaimTypes.IsAdministrator, context.IsAdministrator ? "true" : "false"),
        };
        claims.AddRange(context.ShipCodes.Select(code => new Claim(AppClaimTypes.Ship, code)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, AppClaimTypes.UserId, AppClaimTypes.Role));
    }
}
