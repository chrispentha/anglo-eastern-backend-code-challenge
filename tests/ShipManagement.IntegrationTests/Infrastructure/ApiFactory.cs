using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace ShipManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against the test database, connected as the least-privilege login.
/// Logs are captured so tests can assert that no personal data or secrets are written (SEC-09).
/// </summary>
public sealed class ApiFactory(DatabaseFixture db, IReadOnlyDictionary<string, string?>? settings = null, Action<IServiceCollection>? services = null)
    : WebApplicationFactory<Program>
{
    public FakeLogCollector Logs => Services.GetRequiredService<FakeLogCollector>();

    public HttpClient CreateClient(string? apiKey)
    {
        var client = CreateClient();
        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", db.AppConnectionString);
        builder.UseSetting("RateLimiting:PermitLimitPerUser", "10000");
        builder.UseSetting("RateLimiting:PermitLimitAnonymous", "10000");
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddFakeLogging());
        if (services is not null)
        {
            builder.ConfigureServices(services);
        }
    }
}
