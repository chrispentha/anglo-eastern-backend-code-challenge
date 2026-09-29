using Dapper;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace ShipManagement.IntegrationTests.Infrastructure;

/// <summary>
/// One SQL Server 2022 container per test run. The database is built with the same database/deploy.sh used by
/// docker compose and CI (schema, functions, procedures, least-privilege login, sample data and its verification),
/// then the deterministic test fixtures are loaded.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string DatabaseName = "ShipManagement";
    public const string AppLogin = "ship_api";
    private const string SaPassword = "Test_Sa_Passw0rd!";
    private const string AppPassword = "Test_App_Passw0rd!";

    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword(SaPassword)
        .Build();

    /// <summary>sa connection to the application database (fixtures and assertions only).</summary>
    public string AdminConnectionString { get; private set; } = string.Empty;

    /// <summary>Least-privilege API login: EXECUTE on schema [app] only. This is what the API uses.</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await CopyDatabaseScriptsAsync();
        await RunDeployAsync();

        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = DatabaseName };
        AdminConnectionString = builder.ConnectionString;
        AppConnectionString = new SqlConnectionStringBuilder(AdminConnectionString)
        {
            UserID = AppLogin,
            Password = AppPassword,
            ApplicationName = "ShipManagement.IntegrationTests",
        }.ConnectionString;

        await ExecuteAdminAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "test_data.sql")));
    }

    private ApiFactory? _api;

    /// <summary>The API with default settings, shared by the tests of the collection (started on first use).</summary>
    public ApiFactory Api => _api ??= new ApiFactory(this);

    public async Task DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>Output of the first deployment (contains the randomly generated development API keys).</summary>
    public string InitialDeployOutput { get; private set; } = string.Empty;

    /// <summary>Runs database/deploy.sh inside the container and returns its output; throws when it fails.</summary>
    public async Task<string> RunDeployAsync(bool rotateDevKeys = false)
    {
        var deploy = await _container.ExecAsync(
        [
            "bash",
            "-c",
            $"MSSQL_SA_PASSWORD='{SaPassword}' APP_DB_USER={AppLogin} APP_DB_PASSWORD='{AppPassword}' "
            + $"DB_NAME={DatabaseName} ROTATE_DEV_KEYS={(rotateDevKeys ? 1 : 0)} bash /tmp/database/deploy.sh",
        ]);
        if (deploy.ExitCode != 0)
        {
            throw new InvalidOperationException($"deploy.sh failed ({deploy.ExitCode}):\n{deploy.Stdout}\n{deploy.Stderr}");
        }

        if (InitialDeployOutput.Length == 0)
        {
            InitialDeployOutput = deploy.Stdout;
        }

        return deploy.Stdout;
    }

    public async Task ExecuteAdminAsync(string sql, object? parameters = null)
    {
        await using var connection = new SqlConnection(AdminConnectionString);
        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<T> QueryAdminScalarAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new SqlConnection(AdminConnectionString);
        return await connection.ExecuteScalarAsync<T>(sql, parameters) ?? throw new InvalidOperationException("No value.");
    }

    public Task<int> UserIdAsync(string email) =>
        QueryAdminScalarAsync<int>("SELECT UserId FROM dbo.AppUser WHERE Email = @email", new { email });

    /// <summary>Copies database/ into the container file by file (parent directories are created traversable).</summary>
    private async Task CopyDatabaseScriptsAsync()
    {
        var root = RepositoryPaths.DatabaseDirectory;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            await _container.CopyAsync(
                await File.ReadAllBytesAsync(file),
                "/tmp/database/" + relative);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Integration";
}

internal static class RepositoryPaths
{
    /// <summary>The repository's database/ folder, found by walking up from the test output directory.</summary>
    public static string DatabaseDirectory
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "database", "deploy.sh");
                if (File.Exists(candidate))
                {
                    return Path.GetDirectoryName(candidate)!;
                }
            }

            throw new DirectoryNotFoundException("Could not find database/deploy.sh above the test output directory.");
        }
    }
}
