using System.ComponentModel.DataAnnotations;

namespace ShipManagement.Infrastructure.Persistence;

/// <summary>Database settings, bound from the "Database" configuration section and validated at startup.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Connection string of the least-privilege API login (EXECUTE on schema [app] only, SEC-05).
    /// Supplied via environment variable or user secrets, never committed (SEC-02).
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Per-command timeout; bounds the cost of any single request (SEC-10).</summary>
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 15;
}
