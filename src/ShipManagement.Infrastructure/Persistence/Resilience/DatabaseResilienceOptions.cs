using System.ComponentModel.DataAnnotations;

namespace ShipManagement.Infrastructure.Persistence.Resilience;

/// <summary>Transient-fault handling settings, bound from "Database:Resilience" and validated at startup (D-31).</summary>
public sealed class DatabaseResilienceOptions
{
    public const string SectionName = "Database:Resilience";

    public bool Enabled { get; set; } = true;

    /// <summary>Retries of reads and idempotent writes on transient errors. Non-idempotent writes are never retried.</summary>
    [Range(0, 5)]
    public int RetryCount { get; set; } = 2;

    /// <summary>First retry delay; later retries back off exponentially, with jitter.</summary>
    [Range(0, 5000)]
    public int RetryBaseDelayMilliseconds { get; set; } = 200;

    /// <summary>Share of failed calls (outages only) within the sampling window that opens the circuit.</summary>
    [Range(0.1, 1.0)]
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>Minimum calls in the sampling window before the failure ratio is evaluated.</summary>
    [Range(2, 10_000)]
    public int MinimumThroughput { get; set; } = 10;

    [Range(1, 600)]
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>How long the circuit stays open (requests fail fast with 503) before a trial call is allowed.</summary>
    [Range(1, 600)]
    public int BreakDurationSeconds { get; set; } = 15;
}
