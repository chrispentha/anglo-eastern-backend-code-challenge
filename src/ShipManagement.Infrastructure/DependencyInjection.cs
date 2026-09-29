using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipManagement.Application.ApiKeys;
using ShipManagement.Application.Assignments;
using ShipManagement.Application.Crew;
using ShipManagement.Application.FinancialReports;
using ShipManagement.Application.Security;
using ShipManagement.Application.Ships;
using ShipManagement.Application.Users;
using ShipManagement.Infrastructure.Persistence;
using ShipManagement.Infrastructure.Persistence.Repositories;
using ShipManagement.Infrastructure.Persistence.Resilience;
using ShipManagement.Infrastructure.Security;

namespace ShipManagement.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers data access (stored-procedure repositories), API-key security and the database health check.
    /// Everything here is stateless or thread-safe, so singletons: no per-request allocations for plumbing.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseResilienceOptions>()
            .Bind(configuration.GetSection(DatabaseResilienceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DatabaseResilience>();
        services.AddSingleton<StoredProcedureExecutor>();

        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IShipRepository, ShipRepository>();
        services.AddSingleton<IUserShipRepository, UserShipRepository>();
        services.AddSingleton<ICrewRepository, CrewRepository>();
        services.AddSingleton<IFinancialReportRepository, FinancialReportRepository>();
        services.AddSingleton<IApiKeyRepository, ApiKeyRepository>();
        services.AddSingleton<AuthRepository>();

        services.AddSingleton<IApiKeyGenerator, ApiKeyGenerator>();
        services.AddSingleton<AuthContextResolver>();
        services.AddSingleton<IAuthContextResolver>(sp => sp.GetRequiredService<AuthContextResolver>());
        services.AddSingleton<IAuthContextInvalidator>(sp => sp.GetRequiredService<AuthContextResolver>());

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }
}
