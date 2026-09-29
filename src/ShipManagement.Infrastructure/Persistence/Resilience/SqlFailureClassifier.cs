using Microsoft.Data.SqlClient;

namespace ShipManagement.Infrastructure.Persistence.Resilience;

/// <summary>How a database failure should be treated by the resilience pipeline (D-31).</summary>
public enum SqlFailureKind
{
    /// <summary>Not a database failure, or an error that says nothing about database health (e.g. a bug).</summary>
    Other,

    /// <summary>A business rule raised by a procedure (THROW 5000x) or a constraint: the database is healthy.</summary>
    Business,

    /// <summary>Short-lived and safe to retry: deadlock victim, Azure SQL failover or throttling, dropped connection.</summary>
    Transient,

    /// <summary>The database cannot serve requests right now (unreachable, login refused, timeout, pool exhausted).</summary>
    Unavailable,
}

/// <summary>Classifies exceptions by SQL Server error number. Only Transient and Unavailable count as outages.</summary>
public static class SqlFailureClassifier
{
    // Deadlock victim, and the transient error numbers Microsoft documents for Azure SQL Database
    // (failover, reconfiguration, throttling) plus dropped / reset connections.
    private static readonly HashSet<int> TransientErrors =
    [
        1205,  // deadlock victim
        40197, // service error while processing the request (failover)
        40501, // service busy
        40613, // database not currently available (failover)
        49918, // not enough resources to process the request
        49919, // too many create/update operations in progress
        49920, // too many operations in progress
        4221,  // login to read-secondary failed (replica failover)
        10928, // resource limit reached
        10929, // resource limit reached (minimum guarantee)
        10053, // transport-level error: connection aborted
        10054, // transport-level error: connection reset
        233,   // connection broken
        64,    // specified network name no longer available
        20,    // instance does not support encryption / connection dropped
        121,   // semaphore timeout: connection broken
    ];

    private static readonly HashSet<int> UnavailableErrors =
    [
        -2,    // command timeout
        -1,    // error locating server
        2,     // server not found / not accessible
        53,    // network path not found
        11001, // host not found
        10060, // connection attempt timed out
        10061, // connection refused
        4060,  // cannot open database
        18456, // login failed
    ];

    public static SqlFailureKind Classify(Exception exception) => exception switch
    {
        SqlException sql => Classify(sql.Number),
        TimeoutException => SqlFailureKind.Unavailable,
        // SqlClient reports pool exhaustion as InvalidOperationException ("...obtaining a connection from the pool").
        InvalidOperationException invalid when invalid.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase)
            => SqlFailureKind.Unavailable,
        _ => SqlFailureKind.Other,
    };

    public static SqlFailureKind Classify(int errorNumber) => errorNumber switch
    {
        >= 50000 and <= 50999 => SqlFailureKind.Business,
        SqlErrorMapper.UniqueConstraintViolation or SqlErrorMapper.UniqueIndexViolation or SqlErrorMapper.ConstraintViolation
            => SqlFailureKind.Business,
        _ when TransientErrors.Contains(errorNumber) => SqlFailureKind.Transient,
        _ when UnavailableErrors.Contains(errorNumber) => SqlFailureKind.Unavailable,
        _ => SqlFailureKind.Other,
    };

    /// <summary>True for failures that mean "the database is having trouble", which feed the circuit breaker.</summary>
    public static bool IsOutage(SqlFailureKind kind) => kind is SqlFailureKind.Transient or SqlFailureKind.Unavailable;
}
