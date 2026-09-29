using System.Reflection;
using Microsoft.OpenApi.Models;
using ShipManagement.Api.Auth;

namespace ShipManagement.Api.Swagger;

internal static class SwaggerSetup
{
    public const string ConfigKey = "Swagger:Enabled";

    /// <summary>OpenAPI document with XML comments and the API-key (and optional bearer) security schemes (N4).</summary>
    public static IServiceCollection AddApiSwagger(this IServiceCollection services, bool jwtEnabled)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Ship Management API",
                Version = "v1",
                Description = "Users, ships, ship assignments, crew lists and financial expense reports. "
                    + "Every endpoint except /health requires the X-Api-Key header (or a bearer token when enabled). "
                    + "Errors are RFC 9457 problem documents with a stable 'code' and a 'traceId'.",
            });

            foreach (var assembly in new[] { Assembly.GetExecutingAssembly().GetName().Name, "ShipManagement.Application" })
            {
                var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{assembly}.xml");
                if (File.Exists(xmlFile))
                {
                    options.IncludeXmlComments(xmlFile);
                }
            }

            options.AddSecurityDefinition(AuthSchemes.ApiKey, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = AuthSchemes.ApiKeyHeader,
                Description = "API key issued by an administrator, e.g. sm_devadm01_...",
            });
            options.AddSecurityRequirement(Requirement(AuthSchemes.ApiKey));

            if (jwtEnabled)
            {
                options.AddSecurityDefinition(AuthSchemes.Bearer, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token from the configured identity provider.",
                });
                options.AddSecurityRequirement(Requirement(AuthSchemes.Bearer));
            }
        });

        return services;
    }

    private static OpenApiSecurityRequirement Requirement(string schemeId) => new()
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = schemeId } }] = [],
    };
}
