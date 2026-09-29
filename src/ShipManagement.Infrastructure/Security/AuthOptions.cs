using System.ComponentModel.DataAnnotations;

namespace ShipManagement.Infrastructure.Security;

/// <summary>Authentication settings, bound from the "Auth" configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// How long a resolved caller context is cached (D-25). Changes made through this API invalidate it at once;
    /// changes made elsewhere (or on another instance) take effect within this window. 0 disables caching.
    /// </summary>
    [Range(0, 300)]
    public int CacheSeconds { get; set; } = 30;

    public JwtOptions Jwt { get; set; } = new();
}

/// <summary>Optional JWT bearer authentication for an external identity provider (SEC-03).</summary>
public sealed class JwtOptions
{
    public bool Enabled { get; set; }

    /// <summary>OpenID Connect authority (issuer base URL) used to discover signing keys.</summary>
    public string? Authority { get; set; }

    public string? Issuer { get; set; }

    public string? Audience { get; set; }
}
