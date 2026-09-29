using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence;

/// <summary>
/// Readiness probe: the API can reach the database with its least-privilege login and execute a procedure.
/// Reports no details (server names, versions, errors) to the caller (SEC-11).
/// </summary>
internal sealed class DatabaseHealthCheck(StoredProcedureExecutor executor) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await executor.QuerySingleAsync<bool>("app.usp_Health_Ping", new DynamicParameters(), CommandKind.Read, cancellationToken);
            return healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
#pragma warning disable CA1031 // A health check must report failure, never throw.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
