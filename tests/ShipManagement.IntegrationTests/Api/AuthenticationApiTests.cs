using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Api;

/// <summary>SEC-03 authentication and SEC-04 authorization through the real HTTP pipeline.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthenticationApiTests(DatabaseFixture db)
{
    [Fact]
    public async Task Health_Endpoints_Are_Anonymous_And_Reveal_Nothing()
    {
        var client = db.Api.CreateClient(apiKey: null);

        var live = await client.GetAsync("/health");
        var ready = await client.GetAsync("/health/ready");

        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Request_Without_Credentials_Is_Unauthorized_With_A_Challenge()
    {
        var response = await db.Api.CreateClient(apiKey: null).GetAsync("/api/v1/ships");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        response.Headers.WwwAuthenticate.ToString().Should().Contain("X-Api-Key");
    }

    public static TheoryData<string> RejectedKeys => new()
    {
        "not-a-key",
        "sm_unknown1_" + new string('Z', 43),
        TestData.RevokedKey,
        TestData.ExpiredKey,
        TestData.InactiveUserKey,
    };

    [Theory]
    [MemberData(nameof(RejectedKeys))]
    public async Task Malformed_Unknown_Revoked_Expired_And_Inactive_User_Keys_Fail_Identically(string apiKey)
    {
        var response = await db.Api.CreateClient(apiKey).GetAsync("/api/v1/users/me");

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        problem.GetProperty("detail").GetString().Should().Be("A valid API key (X-Api-Key header) or bearer token is required.");
    }

    [Fact]
    public async Task Valid_Key_Identifies_The_Caller()
    {
        var me = await (await db.Api.CreateClient(TestData.CrewingKey).GetAsync("/api/v1/users/me")).ShouldBeOkJsonAsync();

        me.GetProperty("role").GetString().Should().Be("CrewingOfficer");
        me.GetProperty("email").GetString().Should().Be(TestData.CrewingEmail);
    }

    [Theory]
    [InlineData("GET", "/api/v1/users")]
    [InlineData("POST", "/api/v1/users")]
    [InlineData("POST", "/api/v1/ships")]
    [InlineData("PATCH", "/api/v1/ships/TST01")]
    [InlineData("PUT", "/api/v1/users/1/ships/TST01")]
    [InlineData("DELETE", "/api/v1/users/1/ships/TST01")]
    [InlineData("GET", "/api/v1/users/1/api-keys")]
    public async Task Administrator_Only_Operations_Are_Forbidden_For_Other_Roles(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PATCH")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await db.Api.CreateClient(TestData.CrewingKey).SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [Fact]
    public async Task A_User_Cannot_List_Another_Users_Ships()
    {
        var adminId = await db.UserIdAsync(TestData.AdminEmail);
        var response = await db.Api.CreateClient(TestData.CrewingKey).GetAsync($"/api/v1/users/{adminId}/ships");
        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [Fact]
    public async Task Issued_Key_Works_At_Once_And_Revocation_Takes_Effect_At_Once()
    {
        var admin = db.Api.CreateClient(TestData.AdminKey);
        var otherId = await db.UserIdAsync(TestData.OtherEmail);

        var created = await (await admin.PostAsJsonAsync($"/api/v1/users/{otherId}/api-keys", new { expiresInDays = 1 }))
            .ShouldBeOkJsonAsync(HttpStatusCode.Created);
        var newKey = created.GetProperty("apiKey").GetString()!;
        var keyId = created.GetProperty("metadata").GetProperty("apiKeyId").GetInt32();

        (await db.Api.CreateClient(newKey).GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var keys = await (await admin.GetAsync($"/api/v1/users/{otherId}/api-keys")).ShouldBeOkJsonAsync();
        keys.EnumerateArray().Should().Contain(k => k.GetProperty("apiKeyId").GetInt32() == keyId);
        keys.GetRawText().Should().NotContain(newKey);

        (await admin.DeleteAsync($"/api/v1/users/{otherId}/api-keys/{keyId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await db.Api.CreateClient(newKey).GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Api_Key_Creation_For_Unknown_User_Is_Not_Found()
    {
        var response = await db.Api.CreateClient(TestData.AdminKey).PostAsJsonAsync("/api/v1/users/999999/api-keys", new { });
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "USER_NOT_FOUND");
    }

    [Fact]
    public async Task Api_Key_Creation_Accepts_An_Empty_Body()
    {
        var otherId = await db.UserIdAsync(TestData.OtherEmail);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/users/{otherId}/api-keys");

        var created = await (await db.Api.CreateClient(TestData.AdminKey).SendAsync(request)).ShouldBeOkJsonAsync(HttpStatusCode.Created);

        created.GetProperty("metadata").GetProperty("expiresAtUtc").GetDateTime()
            .Should().BeCloseTo(DateTime.UtcNow.AddDays(90), TimeSpan.FromMinutes(5));
        JsonSerializer.Serialize(created).Should().NotContain("keyHash");
    }
}
