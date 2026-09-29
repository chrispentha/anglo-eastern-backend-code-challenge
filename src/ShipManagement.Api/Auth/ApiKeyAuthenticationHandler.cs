using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShipManagement.Api.Errors;
using ShipManagement.Application.Security;
using ShipManagement.Domain.Errors;
using ShipManagement.Infrastructure.Security;

namespace ShipManagement.Api.Auth;

/// <summary>
/// Authenticates the <c>X-Api-Key</c> header (SEC-03). Unknown, malformed, revoked and expired keys and
/// inactive users all fail identically, so the response never reveals whether a key exists.
/// </summary>
internal sealed partial class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IAuthContextResolver resolver,
    IProblemDetailsService problemDetails)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthSchemes.ApiKeyHeader, out var values))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = values.Count == 1 ? values[0] : null;
        var context = apiKey is null ? null : await resolver.ResolveApiKeyAsync(apiKey, Context.RequestAborted);
        if (context is null)
        {
            // Only the non-secret prefix is logged, and only when the key is well-formed (SEC-09).
            var prefix = ApiKeyFormat.IsWellFormed(apiKey) ? ApiKeyFormat.PrefixOf(apiKey!) : "(malformed)";
            LogAuthenticationFailed(Logger, prefix, Context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var principal = PrincipalFactory.Create(context, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = $"{AuthSchemes.ApiKey} header=\"{AuthSchemes.ApiKeyHeader}\"";
        return problemDetails.WriteProblemAsync(
            Context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized,
            "A valid API key (X-Api-Key header) or bearer token is required.");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        problemDetails.WriteProblemAsync(
            Context, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden,
            "You are not allowed to perform this operation.");

    [LoggerMessage(EventId = 9001, Level = LogLevel.Warning,
        Message = "API key authentication failed (key prefix {KeyPrefix}, remote address {RemoteAddress})")]
    private static partial void LogAuthenticationFailed(ILogger logger, string keyPrefix, string remoteAddress);
}
