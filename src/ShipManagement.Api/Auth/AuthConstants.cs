namespace ShipManagement.Api.Auth;

/// <summary>Claim types carried by an authenticated principal.</summary>
internal static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string Role = "role";
    public const string IsAdministrator = "is_admin";
    public const string Ship = "ship";
}

/// <summary>Authentication scheme names.</summary>
internal static class AuthSchemes
{
    /// <summary>Default scheme: forwards to <see cref="Bearer"/> for "Authorization: Bearer" requests, else <see cref="ApiKey"/>.</summary>
    public const string Default = "ApiKeyOrBearer";
    public const string ApiKey = "ApiKey";
    public const string Bearer = "Bearer";
    public const string ApiKeyHeader = "X-Api-Key";
}

/// <summary>Authorization policy names (SEC-04: function-level authorization through policies, not ad-hoc checks).</summary>
public static class AuthPolicies
{
    public const string Administrator = "Administrator";
}
