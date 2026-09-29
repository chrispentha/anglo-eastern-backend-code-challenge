using FluentValidation;
using Microsoft.Extensions.Logging;
using ShipManagement.Application.Common;
using ShipManagement.Application.Security;

namespace ShipManagement.Application.ApiKeys;

/// <summary>API key metadata. The key itself and its hash are never returned after creation.</summary>
public sealed record ApiKeyDto(
    int ApiKeyId, int UserId, string KeyPrefix, DateTime CreatedAtUtc, DateTime? ExpiresAtUtc, DateTime? RevokedAtUtc, DateTime? LastUsedAtUtc);

/// <summary>A newly created key. <see cref="ApiKey"/> is shown exactly once; store it securely.</summary>
public sealed record CreatedApiKeyDto(string ApiKey, ApiKeyDto Metadata);

/// <summary>Request body for issuing an API key.</summary>
public sealed class CreateApiKeyRequest
{
    /// <summary>Lifetime in days, 1-365. Default 90.</summary>
    public int? ExpiresInDays { get; init; }
}

public interface IApiKeyRepository
{
    Task<ApiKeyDto> CreateAsync(
        int userId, string keyPrefix, byte[] keyHash, DateTime expiresAtUtc, int requestedByUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiKeyDto>> ListByUserAsync(int userId, int requestedByUserId, CancellationToken cancellationToken);

    Task RevokeAsync(int userId, int apiKeyId, int requestedByUserId, CancellationToken cancellationToken);
}

public sealed class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public const int MaxLifetimeDays = 365;

    public CreateApiKeyRequestValidator()
    {
        RuleFor(r => r.ExpiresInDays)
            .InclusiveBetween(1, MaxLifetimeDays)
            .WithMessage($"expiresInDays must be between 1 and {MaxLifetimeDays}.");
    }
}

/// <summary>Issue, list and revoke API keys (SEC-03, D-24). Administrator only.</summary>
public sealed partial class ApiKeyService(
    IApiKeyRepository repository,
    IApiKeyGenerator generator,
    ICurrentUser currentUser,
    IAuthContextInvalidator authContextInvalidator,
    IValidator<CreateApiKeyRequest> validator,
    TimeProvider timeProvider,
    ILogger<ApiKeyService> logger)
{
    public const int DefaultLifetimeDays = 90;

    public async Task<CreatedApiKeyDto> CreateAsync(int userId, CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        ValidationExtensions.EnsurePositiveId(userId, "userId");
        await validator.EnsureValidAsync(request, cancellationToken);

        var key = generator.Generate();
        var expiresAtUtc = timeProvider.GetUtcNow().UtcDateTime.AddDays(request.ExpiresInDays ?? DefaultLifetimeDays);

        var metadata = await repository.CreateAsync(userId, key.Prefix, key.Hash, expiresAtUtc, currentUser.UserId, cancellationToken);

        // Only the non-secret prefix is ever logged (SEC-09).
        LogApiKeyCreated(logger, metadata.ApiKeyId, key.Prefix, userId, currentUser.UserId);
        return new CreatedApiKeyDto(key.RawKey, metadata);
    }

    public Task<IReadOnlyList<ApiKeyDto>> ListAsync(int userId, CancellationToken cancellationToken)
    {
        ValidationExtensions.EnsurePositiveId(userId, "userId");
        return repository.ListByUserAsync(userId, currentUser.UserId, cancellationToken);
    }

    public async Task RevokeAsync(int userId, int apiKeyId, CancellationToken cancellationToken)
    {
        ValidationExtensions.EnsurePositiveId(userId, "userId");
        ValidationExtensions.EnsurePositiveId(apiKeyId, "apiKeyId");

        await repository.RevokeAsync(userId, apiKeyId, currentUser.UserId, cancellationToken);

        // Revocation takes effect immediately on this instance (D-25).
        authContextInvalidator.Invalidate(userId);
        LogApiKeyRevoked(logger, apiKeyId, userId, currentUser.UserId);
    }

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information,
        Message = "API key {ApiKeyId} ({KeyPrefix}) issued for user {UserId} by user {ActorUserId}")]
    private static partial void LogApiKeyCreated(ILogger logger, int apiKeyId, string keyPrefix, int userId, int actorUserId);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Warning,
        Message = "API key {ApiKeyId} of user {UserId} revoked by user {ActorUserId}")]
    private static partial void LogApiKeyRevoked(ILogger logger, int apiKeyId, int userId, int actorUserId);
}
