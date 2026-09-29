using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ShipManagement.Api.Auth;
using ShipManagement.Api.Errors;
using ShipManagement.Domain.Errors;

namespace ShipManagement.Api.Middleware;

/// <summary>Rate-limit settings, bound from the "RateLimiting" configuration section (SEC-10).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Requests per window per authenticated user.</summary>
    [Range(1, 100_000)]
    public int PermitLimitPerUser { get; set; } = 300;

    /// <summary>Requests per window per client address for unauthenticated traffic (e.g. key guessing).</summary>
    [Range(1, 100_000)]
    public int PermitLimitAnonymous { get; set; } = 60;
}

internal static partial class RateLimitingSetup
{
    /// <summary>
    /// Fixed-window limiter partitioned by user id (authenticated) or client address (anonymous).
    /// Rejections return 429 with Retry-After and a problem document, and are logged as security events.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                if (!settings.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                var userId = context.User.FindFirstValue(AppClaimTypes.UserId);
                var (key, permits) = userId is not null
                    ? ("user:" + userId, settings.PermitLimitPerUser)
                    : ("ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"), settings.PermitLimitAnonymous);

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0,
                });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
                    ? (int)Math.Ceiling(wait.TotalSeconds)
                    : settings.WindowSeconds;
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting");
                LogRateLimited(logger, context.HttpContext.User.FindFirstValue(AppClaimTypes.UserId) ?? "anonymous");

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteProblemAsync(
                    context.HttpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited,
                    $"Too many requests. Retry after {retryAfter} seconds.");
            };
        });

        return services;
    }

    [LoggerMessage(EventId = 9301, Level = LogLevel.Warning, Message = "Rate limit exceeded for user {UserId}")]
    private static partial void LogRateLimited(ILogger logger, string userId);
}
