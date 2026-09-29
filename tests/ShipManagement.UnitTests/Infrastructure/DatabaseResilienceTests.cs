using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using ShipManagement.Domain.Errors;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.UnitTests.Infrastructure;

/// <summary>Retry and circuit-breaker behaviour of every database call (D-31), with a fake clock.</summary>
public class DatabaseResilienceTests
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private sealed class TransientFailure : Exception;

    private sealed class Outage : Exception;

    private sealed class BusinessError : Exception;

    private static SqlFailureKind Classify(Exception ex) => ex switch
    {
        TransientFailure => SqlFailureKind.Transient,
        Outage => SqlFailureKind.Unavailable,
        BusinessError => SqlFailureKind.Business,
        _ => SqlFailureKind.Other,
    };

    private DatabaseResilience Create(int retryCount = 2, int minimumThroughput = 2, bool enabled = true) => new(
        new DatabaseResilienceOptions
        {
            Enabled = enabled,
            RetryCount = retryCount,
            RetryBaseDelayMilliseconds = 0,
            FailureRatio = 0.5,
            MinimumThroughput = minimumThroughput,
            SamplingDurationSeconds = 30,
            BreakDurationSeconds = 15,
        },
        _clock,
        Classify);

    /// <summary>A call that fails with the given exceptions, in order, then succeeds.</summary>
    private static Func<CancellationToken, Task<int>> FailingThenSucceeding(Counter counter, params Exception[] failures) => _ =>
    {
        var attempt = counter.Increment();
        return attempt <= failures.Length ? Task.FromException<int>(failures[attempt - 1]) : Task.FromResult(42);
    };

    private sealed class Counter
    {
        private int _value;

        public int Value => _value;

        public int Increment() => Interlocked.Increment(ref _value);
    }

    [Theory]
    [InlineData(CommandKind.Read)]
    [InlineData(CommandKind.IdempotentWrite)]
    public async Task Safe_Calls_Are_Retried_On_Transient_Errors(CommandKind kind)
    {
        var calls = new Counter();

        var result = await Create(minimumThroughput: 100).ExecuteAsync(
            FailingThenSucceeding(calls, new TransientFailure(), new TransientFailure()), kind, CancellationToken.None);

        result.Should().Be(42);
        calls.Value.Should().Be(3);
    }

    [Fact]
    public async Task Writes_Are_Never_Retried()
    {
        var calls = new Counter();

        var act = () => Create(minimumThroughput: 100).ExecuteAsync(
            FailingThenSucceeding(calls, new TransientFailure()), CommandKind.Write, CancellationToken.None);

        await act.Should().ThrowAsync<TransientFailure>();
        calls.Value.Should().Be(1);
    }

    [Fact]
    public async Task Retries_Stop_After_The_Configured_Count()
    {
        var calls = new Counter();

        var act = () => Create(retryCount: 2, minimumThroughput: 100).ExecuteAsync(
            FailingThenSucceeding(calls, new TransientFailure(), new TransientFailure(), new TransientFailure()),
            CommandKind.Read, CancellationToken.None);

        await act.Should().ThrowAsync<TransientFailure>();
        calls.Value.Should().Be(3);
    }

    [Fact]
    public async Task Retry_Can_Be_Switched_Off()
    {
        var calls = new Counter();

        var act = () => Create(retryCount: 0, minimumThroughput: 100).ExecuteAsync(
            FailingThenSucceeding(calls, new TransientFailure()), CommandKind.Read, CancellationToken.None);

        await act.Should().ThrowAsync<TransientFailure>();
        calls.Value.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(NonTransient))]
    public async Task Non_Transient_Errors_Are_Not_Retried(Exception error)
    {
        var calls = new Counter();

        var act = () => Create(minimumThroughput: 100).ExecuteAsync(
            FailingThenSucceeding(calls, error), CommandKind.Read, CancellationToken.None);

        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeSameAs(error);
        calls.Value.Should().Be(1);
    }

    public static TheoryData<Exception> NonTransient => new() { new Outage(), new BusinessError(), new InvalidOperationException("bug") };

    [Fact]
    public async Task Circuit_Opens_After_Repeated_Outages_And_Then_Fails_Fast()
    {
        var resilience = Create(retryCount: 0, minimumThroughput: 2);
        var calls = new Counter();
        Func<CancellationToken, Task<int>> outage = _ =>
        {
            calls.Increment();
            return Task.FromException<int>(new Outage());
        };

        await FluentActions.Awaiting(() => resilience.ExecuteAsync(outage, CommandKind.Read, CancellationToken.None)).Should().ThrowAsync<Outage>();
        await FluentActions.Awaiting(() => resilience.ExecuteAsync(outage, CommandKind.Write, CancellationToken.None)).Should().ThrowAsync<Outage>();

        // Open: the database is not called at all, the caller gets a 503 with a Retry-After hint.
        var open = await FluentActions.Awaiting(() => resilience.ExecuteAsync(outage, CommandKind.Read, CancellationToken.None))
            .Should().ThrowAsync<DependencyUnavailableException>();
        open.Which.Code.Should().Be(ErrorCodes.DatabaseUnavailable);
        open.Which.RetryAfter.Should().BePositive().And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(15));
        calls.Value.Should().Be(2);
    }

    [Fact]
    public async Task Circuit_Closes_Again_When_A_Trial_Call_Succeeds_After_The_Break()
    {
        var resilience = Create(retryCount: 0, minimumThroughput: 2);
        for (var i = 0; i < 2; i++)
        {
            await FluentActions.Awaiting(() => resilience.ExecuteAsync<int>(_ => throw new Outage(), CommandKind.Read, CancellationToken.None))
                .Should().ThrowAsync<Outage>();
        }

        _clock.Advance(TimeSpan.FromSeconds(16));

        (await resilience.ExecuteAsync(_ => Task.FromResult(7), CommandKind.Read, CancellationToken.None)).Should().Be(7);
        (await resilience.ExecuteAsync(_ => Task.FromResult(8), CommandKind.Read, CancellationToken.None)).Should().Be(8);
    }

    [Fact]
    public async Task Business_Errors_Never_Open_The_Circuit()
    {
        var resilience = Create(retryCount: 0, minimumThroughput: 2);
        for (var i = 0; i < 10; i++)
        {
            await FluentActions.Awaiting(() => resilience.ExecuteAsync<int>(_ => throw new BusinessError(), CommandKind.Read, CancellationToken.None))
                .Should().ThrowAsync<BusinessError>();
        }

        (await resilience.ExecuteAsync(_ => Task.FromResult(1), CommandKind.Read, CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task Disabled_Resilience_Calls_Straight_Through()
    {
        var calls = new Counter();

        var act = () => Create(enabled: false).ExecuteAsync(
            FailingThenSucceeding(calls, new TransientFailure()), CommandKind.Read, CancellationToken.None);

        await act.Should().ThrowAsync<TransientFailure>();
        calls.Value.Should().Be(1);
    }
}
