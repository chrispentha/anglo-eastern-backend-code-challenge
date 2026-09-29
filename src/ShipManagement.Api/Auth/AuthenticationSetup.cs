using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using ShipManagement.Api.Errors;
using ShipManagement.Application.Common;
using ShipManagement.Application.Security;
using ShipManagement.Domain.Errors;
using ShipManagement.Infrastructure.Security;

namespace ShipManagement.Api.Auth;

internal static class AuthenticationSetup
{
    /// <summary>
    /// API-key authentication for every request, plus optional JWT bearer for an external identity provider (SEC-03).
    /// Every endpoint requires an authenticated caller unless it opts out explicitly ([AllowAnonymous]).
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()?.Jwt ?? new JwtOptions();

        var authentication = services
            .AddAuthentication(AuthSchemes.Default)
            .AddPolicyScheme(AuthSchemes.Default, "API key or bearer token", options =>
            {
                options.ForwardDefaultSelector = context =>
                    jwt.Enabled && context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? AuthSchemes.Bearer
                        : AuthSchemes.ApiKey;
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthSchemes.ApiKey, _ => { });

        if (jwt.Enabled)
        {
            // Blank values count as missing: configuration turns a JSON null into an empty string.
            var authority = NullIfBlank(jwt.Authority);
            var issuer = NullIfBlank(jwt.Issuer) ?? authority;
            var audience = NullIfBlank(jwt.Audience);
            if (issuer is null || audience is null)
            {
                // Fail fast: a half-configured identity provider must never start accepting tokens.
                throw new InvalidOperationException("Auth:Jwt is enabled but Auth:Jwt:Audience and Auth:Jwt:Issuer (or Authority) are not both set.");
            }

            authentication.AddJwtBearer(AuthSchemes.Bearer, options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidIssuer = issuer;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.RequireSignedTokens = true;
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
                options.Events = new JwtBearerEvents
                {
                    // A valid token is not enough: its subject must be an active user of this system.
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst(AppClaimTypes.UserId)?.Value;
                        var resolver = context.HttpContext.RequestServices.GetRequiredService<IAuthContextResolver>();
                        var authContext = int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
                            ? await resolver.ResolveUserAsync(userId, context.HttpContext.RequestAborted)
                            : null;

                        if (authContext is null)
                        {
                            context.Fail("Unknown or inactive user.");
                            return;
                        }

                        context.Principal = PrincipalFactory.Create(authContext, AuthSchemes.Bearer);
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                        await problemDetails.WriteProblemAsync(
                            context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized,
                            "A valid API key (X-Api-Key header) or bearer token is required.");
                    },
                };
            });
        }

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthPolicies.Administrator, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.IsAdministrator, "true"));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
