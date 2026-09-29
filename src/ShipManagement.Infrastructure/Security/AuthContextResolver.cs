using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using ShipManagement.Application.Security;
using ShipManagement.Infrastructure.Persistence.Repositories;

namespace ShipManagement.Infrastructure.Security;

/// <summary>
/// Resolves API keys and JWT subjects to caller contexts, with a short in-process cache (D-25) so an
/// authenticated request normally costs no extra database round trip. Only successful resolutions are
/// cached, so a newly issued key works immediately. Each user's entries share a cancellation token, so
/// <see cref="Invalidate"/> drops all of them at once; a generation counter stops a resolution that
/// raced with an invalidation from re-caching stale data.
/// </summary>
internal sealed class AuthContextResolver(AuthRepository repository, IMemoryCache cache, IOptions<AuthOptions> options)
    : IAuthContextResolver, IAuthContextInvalidator, IDisposable
{
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _userTokens = new();
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(options.Value.CacheSeconds);
    private long _generation;

    public Task<AuthContext?> ResolveApiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (!ApiKeyFormat.IsWellFormed(apiKey))
        {
            return Task.FromResult<AuthContext?>(null);
        }

        var hash = ApiKeyFormat.Hash(apiKey);
        return ResolveAsync("auth:key:" + Convert.ToHexString(hash), ct => repository.ResolveApiKeyAsync(hash, ct), cancellationToken);
    }

    public Task<AuthContext?> ResolveUserAsync(int userId, CancellationToken cancellationToken) =>
        userId <= 0
            ? Task.FromResult<AuthContext?>(null)
            : ResolveAsync($"auth:user:{userId}", ct => repository.ResolveUserAsync(userId, ct), cancellationToken);

    public void Invalidate(int userId)
    {
        Interlocked.Increment(ref _generation);
        if (_userTokens.TryRemove(userId, out var tokenSource))
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var tokenSource in _userTokens.Values)
        {
            tokenSource.Dispose();
        }
    }

    private async Task<AuthContext?> ResolveAsync(
        string cacheKey, Func<CancellationToken, Task<AuthContext?>> load, CancellationToken cancellationToken)
    {
        if (_ttl > TimeSpan.Zero && cache.TryGetValue(cacheKey, out AuthContext? cached))
        {
            return cached;
        }

        var generation = Interlocked.Read(ref _generation);
        var context = await load(cancellationToken);

        if (context is not null && _ttl > TimeSpan.Zero && Interlocked.Read(ref _generation) == generation)
        {
            var tokenSource = _userTokens.GetOrAdd(context.UserId, _ => new CancellationTokenSource());
            var entryOptions = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _ttl };
            entryOptions.AddExpirationToken(new CancellationChangeToken(tokenSource.Token));
            cache.Set(cacheKey, context, entryOptions);
        }

        return context;
    }
}
