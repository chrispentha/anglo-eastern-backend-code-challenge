using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShipManagement.Application.ApiKeys;
using ShipManagement.Application.Assignments;
using ShipManagement.Application.Crew;
using ShipManagement.Application.FinancialReports;
using ShipManagement.Application.Ships;
using ShipManagement.Application.Users;

namespace ShipManagement.Application;

public static class DependencyInjection
{
    /// <summary>Registers use-case services and validators (validators are stateless, so singletons).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<UserService>();
        services.AddScoped<ShipService>();
        services.AddScoped<AssignmentService>();
        services.AddScoped<CrewService>();
        services.AddScoped<FinancialReportService>();
        services.AddScoped<ApiKeyService>();

        return services;
    }
}
