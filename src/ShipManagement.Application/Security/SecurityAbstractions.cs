namespace ShipManagement.Application.Security;

/// <summary>Security context of an authenticated caller, as resolved from the database.</summary>
public sealed record AuthContext(int UserId, string Role, bool IsAdministrator, IReadOnlyList<string> ShipCodes);

/// <summary>Resolves credentials to an <see cref="AuthContext"/>; null means "not authenticated".</summary>
public interface IAuthContextResolver
{
    /// <summary>Resolves a raw API key (SEC-03). Only its SHA-256 hash ever reaches the database.</summary>
    Task<AuthContext?> ResolveApiKeyAsync(string apiKey, CancellationToken cancellationToken);

    /// <summary>Resolves the user id carried by a validated JWT ('sub' claim).</summary>
    Task<AuthContext?> ResolveUserAsync(int userId, CancellationToken cancellationToken);
}

/// <summary>Drops cached security contexts of a user after their ships or keys change (D-25).</summary>
public interface IAuthContextInvalidator
{
    void Invalidate(int userId);
}

/// <summary>A newly generated API key. <see cref="RawKey"/> is shown to the administrator once and never stored.</summary>
public sealed record GeneratedApiKey(string RawKey, string Prefix, byte[] Hash);

/// <summary>Generates API keys from a cryptographically secure random source.</summary>
public interface IApiKeyGenerator
{
    GeneratedApiKey Generate();
}
