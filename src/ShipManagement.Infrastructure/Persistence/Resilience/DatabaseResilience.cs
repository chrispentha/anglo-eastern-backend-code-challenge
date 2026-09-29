using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using ShipManagement.Domain.Errors;

namespace ShipManagement.Infrastructure.Persistence.Resilience;

/// <summary>What a database call does, which decides whether it may be retried.</summary>
public enum CommandKind
{
    /// <summary>Reads only: always safe to retry.</summary>
    Read,

    /// <summary>A write whose repetition has the same effect (assign, unassign, revoke): safe to retry.</summary>
    IdempotentWrite,

    /// <summary>A write that must never run twice (create, update): not retried.</summary>
    Write,
}

/// <summary>
/// Transient-fault handling for every database call (D-31), built with Polly:
/// <list type="bullet">
/// <item>Retry: reads and idempotent writes are retried on transient errors (deadlock victim, Azure SQL failover,
/// dropped connection) with exponential back-off and jitter. Other writes are never retried: a create or update
/// retried after an ambiguous failure could be applied twice.</item>
/// <item>Circuit breaker: when too many calls fail with outage-type errors, the circuit opens and calls fail fast
/// with <see cref="DependencyUnavailableException"/> (HTTP 503 + Retry-After) instead of each waiting for a timeout,
/// which protects the connection pool and gives the database room to recover. Business errors (THROW 5000x,
/// constraint violations) never count as failures. One breaker is shared by all calls (per API instance).</item>
/// </list>
/// </summary>
public sealed class DatabaseResilience
{
    private readonly bool _enabled;
    private readonly TimeSpan _breakDuration;
    private readonly ResiliencePipeline _breaker;
    private readonly ResiliencePipeline _retryThenBreaker;

    public DatabaseResilience(IOptions<DatabaseResilienceOptions> options, TimeProvider timeProvider)
        : this(options.Value, timeProvider, SqlFailureClassifier.Classify)
    {
    }

    internal DatabaseResilience(DatabaseResilienceOptions options, TimeProvider timeProvider, Func<Exception, SqlFailureKind> classify)
    {
        _enabled = options.Enabled;
        _breakDuration = TimeSpan.FromSeconds(options.BreakDurationSeconds);

        _breaker = new ResiliencePipelineBuilder { TimeProvider = timeProvider }
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.FailureRatio,
                MinimumThroughput = options.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.SamplingDurationSeconds),
                BreakDuration = _breakDuration,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => SqlFailureClassifier.IsOutage(classify(ex))),
            })
            .Build();

        // Retry wraps the shared breaker: every attempt is recorded by the breaker, and an open circuit
        // (BrokenCircuitException, classified as Other) is never retried.
        _retryThenBreaker = new ResiliencePipelineBuilder { TimeProvider = timeProvider }
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = Math.Max(1, options.RetryCount),
                Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    options.RetryCount > 0 && classify(ex) == SqlFailureKind.Transient),
            })
            .AddPipeline(_breaker)
            .Build();
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CommandKind kind, CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            return await action(cancellationToken);
        }

        var pipeline = kind == CommandKind.Write ? _breaker : _retryThenBreaker;
        try
        {
            return await pipeline.ExecuteAsync(
                static async (state, token) => await state(token), action, cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            throw new DependencyUnavailableException(
                "The database is temporarily unavailable. Please retry later.", ex.RetryAfter ?? _breakDuration, ex);
        }
    }
}
