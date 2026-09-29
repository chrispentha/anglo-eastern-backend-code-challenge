using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Api;

/// <summary>HTTP hardening (SEC-11), rate limiting (SEC-10), log hygiene (SEC-09), JWT (SEC-03) and Swagger (N4).</summary>
[Collection(IntegrationCollection.Name)]
public sealed class HardeningApiTests(DatabaseFixture db)
{
    [Fact]
    public async Task Responses_Carry_Security_Headers_And_Are_Never_Cached()
    {
        var response = await db.Api.CreateClient(TestData.CrewingKey).GetAsync("/api/v1/ships/TST01/crew");

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Contains("Server").Should().BeFalse();
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task Error_Responses_Also_Carry_Security_Headers()
    {
        var response = await db.Api.CreateClient(apiKey: null).GetAsync("/api/v1/ships");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Fact]
    public async Task Unknown_Route_Is_A_Problem_Document()
    {
        await (await db.Api.CreateClient(TestData.AdminKey).GetAsync("/api/v1/does-not-exist"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task Reports_Are_Compressed_When_The_Client_Accepts_It()
    {
        var client = db.Api.CreateClient(TestData.CrewingKey);
        client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        var response = await client.GetAsync("/api/v1/ships/TST02/financial-reports/detail?period=2025-02");

        response.Content.Headers.ContentEncoding.Should().Contain("br");
    }

    [Fact]
    public async Task Requests_Over_The_Limit_Are_Rejected_With_Retry_After()
    {
        await using var api = new ApiFactory(db, new Dictionary<string, string?> { ["RateLimiting:PermitLimitPerUser"] = "3" });
        var client = api.CreateClient(TestData.RateLimitKey);

        for (var i = 0; i < 3; i++)
        {
            (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var limited = await client.GetAsync("/api/v1/users/me");
        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "RATE_LIMITED");
        limited.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Logs_Never_Contain_Crew_Names_Search_Terms_Or_Api_Keys()
    {
        db.Api.Logs.Clear();

        await db.Api.CreateClient(TestData.CrewingKey).GetAsync("/api/v1/ships/TST01/crew?search=Philippou");
        await db.Api.CreateClient("sm_badbadba_" + new string('Q', 43)).GetAsync("/api/v1/users/me");

        var logs = string.Join('\n', db.Api.Logs.GetSnapshot().Select(r => r.Message + " " + string.Join(' ', r.StructuredState ?? [])));
        logs.Should().Contain("TST01", "requests are logged");
        logs.Should().NotContain("Philippou").And.NotContain("Andreas").And.NotContain("search=");
        logs.Should().NotContain(TestData.CrewingKey).And.NotContain(new string('Q', 43));
    }

    [Fact]
    public async Task Swagger_Document_Is_Served_When_Enabled()
    {
        await using var api = new ApiFactory(db, new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" });

        var response = await api.CreateClient(apiKey: null).GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = await response.Content.ReadAsStringAsync();
        document.Should().Contain("/api/v1/ships/{shipCode}/crew").And.Contain("X-Api-Key");
    }

    [Fact]
    public async Task Swagger_Is_Not_Served_By_Default()
    {
        (await db.Api.CreateClient(apiKey: null).GetAsync("/swagger/v1/swagger.json")).StatusCode
            .Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Jwt_Bearer_Token_For_An_Active_User_Is_Accepted()
    {
        using var rsa = RSA.Create(2048);
        await using var api = JwtApi(rsa);
        var crewingId = await db.UserIdAsync(TestData.CrewingEmail);

        var client = api.CreateClient(apiKey: null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(rsa, crewingId.ToString(CultureInfo.InvariantCulture)));

        var me = await (await client.GetAsync("/api/v1/users/me")).ShouldBeOkJsonAsync();
        me.GetProperty("userId").GetInt32().Should().Be(crewingId);
    }

    [Fact]
    public async Task Jwt_For_Unknown_User_Or_With_Wrong_Signature_Is_Rejected()
    {
        using var rsa = RSA.Create(2048);
        using var attackerKey = RSA.Create(2048);
        await using var api = JwtApi(rsa);
        var crewingId = await db.UserIdAsync(TestData.CrewingEmail);

        var rejected = new[]
        {
            Token(rsa, "999999"),                                                         // unknown user
            Token(rsa, "0"),                                                              // not a valid user id
            Token(rsa, "not-a-number"),                                                   // not a user id at all
            Token(attackerKey, crewingId.ToString(CultureInfo.InvariantCulture)),        // wrong signing key
            Token(rsa, crewingId.ToString(CultureInfo.InvariantCulture), expired: true), // expired
        };
        foreach (var token in rejected)
        {
            var client = api.CreateClient(apiKey: null);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await (await client.GetAsync("/api/v1/users/me")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        }
    }

    [Fact]
    public async Task Api_Keys_Keep_Working_When_Jwt_Is_Enabled()
    {
        using var rsa = RSA.Create(2048);
        await using var api = JwtApi(rsa);

        (await api.CreateClient(TestData.CrewingKey).GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Jwt_Issuer_Defaults_To_The_Authority()
    {
        using var rsa = RSA.Create(2048);
        // A blank Issuer (e.g. a JSON null in appsettings) must fall back to the Authority.
        await using var api = JwtApi(rsa, issuerFromAuthority: true, extraSettings: new() { ["Auth:Jwt:Issuer"] = "" });
        var crewingId = await db.UserIdAsync(TestData.CrewingEmail);

        var client = api.CreateClient(apiKey: null);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token(rsa, crewingId.ToString(CultureInfo.InvariantCulture)));

        (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Half_Configured_Jwt_Refuses_To_Start()
    {
        await using var api = new ApiFactory(db, new Dictionary<string, string?>
        {
            ["Auth:Jwt:Enabled"] = "true",
            ["Auth:Jwt:Issuer"] = Issuer,
        });

        var act = () => api.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Auth:Jwt*Audience*");
    }

    [Fact]
    public async Task Swagger_Documents_The_Bearer_Scheme_When_Jwt_Is_Enabled()
    {
        using var rsa = RSA.Create(2048);
        await using var api = JwtApi(rsa, extraSettings: new() { ["Swagger:Enabled"] = "true" });

        var document = await api.CreateClient(apiKey: null).GetStringAsync("/swagger/v1/swagger.json");

        document.Should().Contain("\"bearer\"").And.Contain("X-Api-Key");
    }

    [Fact]
    public async Task Readiness_Reports_503_When_The_Database_Is_Unreachable()
    {
        var badConnection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(db.AppConnectionString) { Password = "wrong-password" };
        await using var api = new ApiFactory(db, new Dictionary<string, string?> { ["Database:ConnectionString"] = badConnection.ConnectionString });
        var client = api.CreateClient(apiKey: null);

        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        var ready = await client.GetAsync("/health/ready");

        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ready.Content.ReadAsStringAsync()).Should().Be("Unhealthy");
    }

    [Fact]
    public async Task Rate_Limiting_Can_Be_Disabled()
    {
        await using var api = new ApiFactory(db, new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = "false",
            ["RateLimiting:PermitLimitPerUser"] = "1",
        });
        var client = api.CreateClient(TestData.RateLimitKey);

        for (var i = 0; i < 3; i++)
        {
            (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Callers_Are_Resolved_On_Every_Request_When_The_Auth_Cache_Is_Disabled()
    {
        await using var api = new ApiFactory(db, new Dictionary<string, string?> { ["Auth:CacheSeconds"] = "0" });
        var client = api.CreateClient(TestData.CrewingKey);

        (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/ships/TST01/crew")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task More_Than_One_Api_Key_Header_Is_Rejected()
    {
        var client = db.Api.CreateClient(apiKey: null);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.TryAddWithoutValidation("X-Api-Key", [TestData.CrewingKey, TestData.AdminKey]);

        await (await client.SendAsync(request)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Https_Redirection_Can_Be_Enabled_By_Configuration()
    {
        await using var api = new ApiFactory(db, new Dictionary<string, string?>
        {
            ["Security:RequireHttps"] = "true",
            ["HTTPS_PORT"] = "443",
        });
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location!.Scheme.Should().Be("https");
    }

    private const string Issuer = "https://idp.test";
    private const string Audience = "ship-management-api";

    private ApiFactory JwtApi(RSA rsa, bool issuerFromAuthority = false, Dictionary<string, string?>? extraSettings = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Auth:Jwt:Enabled"] = "true",
            ["Auth:Jwt:Audience"] = Audience,
            [issuerFromAuthority ? "Auth:Jwt:Authority" : "Auth:Jwt:Issuer"] = Issuer,
        };
        foreach (var (key, value) in extraSettings ?? [])
        {
            settings[key] = value;
        }

        return JwtApi(rsa, settings);
    }

    private ApiFactory JwtApi(RSA rsa, Dictionary<string, string?> settings) => new(
        db,
        settings,
        services => services.PostConfigure<JwtBearerOptions>("Bearer", options =>
        {
            // Offline signing key instead of OIDC discovery from an authority.
            options.Authority = null;
            options.ConfigurationManager = null;
            options.TokenValidationParameters.ConfigurationManager = null;
            options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false));
        }));

    private static string Token(RSA rsa, string subject, bool expired = false)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity([new Claim("sub", subject)]),
            NotBefore = expired ? now.AddHours(-2) : now.AddMinutes(-1),
            Expires = expired ? now.AddHours(-1) : now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256),
        });
    }
}
