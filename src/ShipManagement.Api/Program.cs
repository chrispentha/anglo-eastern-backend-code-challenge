using System.IO.Compression;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using ShipManagement.Api.Auth;
using ShipManagement.Api.Errors;
using ShipManagement.Api.Middleware;
using ShipManagement.Api.Swagger;
using ShipManagement.Application;
using ShipManagement.Infrastructure;
using ShipManagement.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// ---- Kestrel hardening (SEC-07, SEC-11) ----
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

// ---- Layers ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- HTTP surface ----
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create)
    .AddJsonOptions(options =>
    {
        // Unknown JSON properties are rejected: no mass assignment of server-controlled fields (SEC-07).
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.JsonSerializerOptions.MaxDepth = 16;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsExtensions.AddDefaults);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);

// Report payloads are compressed: ship-to-shore links are slow and expensive.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

var swaggerEnabled = builder.Configuration.GetValue<bool>(SwaggerSetup.ConfigKey);
if (swaggerEnabled)
{
    var jwtEnabled = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()?.Jwt.Enabled ?? false;
    builder.Services.AddApiSwagger(jwtEnabled);
}

var app = builder.Build();

// ---- Pipeline (order matters) ----
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Configuration.GetValue<bool>("Security:RequireHttps"))
{
    app.UseHttpsRedirection();
}

app.UseResponseCompression();
app.UseMiddleware<RequestLoggingMiddleware>();

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "Ship Management API");
}

app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

// Liveness: the process is up (no dependencies). Readiness: the database is reachable. No details exposed.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous()
    .DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous()
    .DisableRateLimiting();

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the API in memory.</summary>
public partial class Program;
