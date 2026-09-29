using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ShipManagement.Domain.Errors;
using ShipManagement.Infrastructure.Persistence.Resilience;

namespace ShipManagement.Infrastructure.Persistence;

/// <summary>
/// The single gateway to the database. Every call is a stored procedure (<see cref="CommandType.StoredProcedure"/>)
/// with typed, sized parameters: no SQL text is ever built in C# (SEC-06). Connections are pooled, opened late
/// and disposed early; the request's cancellation token and a command timeout flow to SQL Server (SEC-10).
/// Every call runs through <see cref="DatabaseResilience"/> (retry of safe calls, circuit breaker, D-31).
/// </summary>
public sealed class StoredProcedureExecutor(IOptions<DatabaseOptions> options, DatabaseResilience resilience)
{
    private static readonly TimeSpan OutageRetryAfter = TimeSpan.FromSeconds(5);

    private readonly DatabaseOptions _options = options.Value;

    public Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedure, DynamicParameters parameters, CommandKind kind, CancellationToken cancellationToken) =>
        RunAsync<IReadOnlyList<T>>(async (connection, token) =>
            (await connection.QueryAsync<T>(Command(procedure, parameters, token))).AsList(), kind, cancellationToken);

    public Task<T> QuerySingleAsync<T>(
        string procedure, DynamicParameters parameters, CommandKind kind, CancellationToken cancellationToken) =>
        RunAsync((connection, token) => connection.QuerySingleAsync<T>(Command(procedure, parameters, token)), kind, cancellationToken);

    public Task ExecuteAsync(string procedure, DynamicParameters parameters, CommandKind kind, CancellationToken cancellationToken) =>
        RunAsync((connection, token) => connection.ExecuteAsync(Command(procedure, parameters, token)), kind, cancellationToken);

    public Task<TResult> QueryMultipleAsync<TResult>(
        string procedure,
        DynamicParameters parameters,
        Func<SqlMapper.GridReader, Task<TResult>> read,
        CommandKind kind,
        CancellationToken cancellationToken) =>
        RunAsync(async (connection, token) =>
        {
            await using var grid = await connection.QueryMultipleAsync(Command(procedure, parameters, token));
            return await read(grid);
        }, kind, cancellationToken);

    /// <summary>
    /// Opens a pooled connection per attempt, runs the call through the resilience pipeline, and translates failures:
    /// business errors to domain exceptions, outages that survived the retries to 503; anything else stays a 500.
    /// </summary>
    private async Task<T> RunAsync<T>(Func<SqlConnection, CancellationToken, Task<T>> call, CommandKind kind, CancellationToken cancellationToken)
    {
        try
        {
            return await resilience.ExecuteAsync(
                async token =>
                {
                    await using var connection = new SqlConnection(_options.ConnectionString);
                    return await call(connection, token);
                },
                kind,
                cancellationToken);
        }
        catch (SqlException ex) when (SqlErrorMapper.TryMap(ex, out var mapped))
        {
            throw mapped;
        }
        catch (Exception ex) when (SqlFailureClassifier.IsOutage(SqlFailureClassifier.Classify(ex)))
        {
            throw new DependencyUnavailableException(
                "The database is temporarily unavailable. Please retry later.", OutageRetryAfter, ex);
        }
    }

    private CommandDefinition Command(string procedure, DynamicParameters parameters, CancellationToken cancellationToken) =>
        new(procedure, parameters, commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);
}
