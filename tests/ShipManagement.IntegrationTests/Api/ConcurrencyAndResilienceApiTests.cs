using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Api;

/// <summary>Optimistic concurrency on ship updates (D-30) and database resilience (D-31) through the real pipeline.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class ConcurrencyAndResilienceApiTests(DatabaseFixture db)
{
    private HttpClient Admin => db.Api.CreateClient(TestData.AdminKey);

    private async Task<string> CreateShipAsync()
    {
        var code = "C" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        (await Admin.PostAsJsonAsync("/api/v1/ships", new { shipCode = code, shipName = "Concurrent", fiscalYearCode = "0112" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        return code;
    }

    private static HttpRequestMessage Patch(string code, object body, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/ships/{code}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }

    [Fact]
    public async Task Get_Returns_The_Version_As_A_Strong_ETag()
    {
        var code = await CreateShipAsync();

        var response = await Admin.GetAsync($"/api/v1/ships/{code}");
        var ship = await response.ShouldBeOkJsonAsync();

        response.Headers.ETag.Should().NotBeNull();
        response.Headers.ETag!.IsWeak.Should().BeFalse();
        response.Headers.ETag.Tag.Should().Be($"\"{ship.GetProperty("version").GetString()}\"");
    }

    [Fact]
    public async Task Update_With_The_Current_ETag_Succeeds_And_Returns_A_New_ETag()
    {
        var code = await CreateShipAsync();
        var etag = (await Admin.GetAsync($"/api/v1/ships/{code}")).Headers.ETag!.Tag;

        var response = await Admin.SendAsync(Patch(code, new { shipName = "Renamed" }, etag));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().NotBe(etag);
    }

    [Fact]
    public async Task Second_Writer_With_A_Stale_ETag_Is_Rejected_Instead_Of_Overwriting()
    {
        var code = await CreateShipAsync();
        var etagSeenByBoth = (await Admin.GetAsync($"/api/v1/ships/{code}")).Headers.ETag!.Tag;

        var first = await Admin.SendAsync(Patch(code, new { shipName = "First writer" }, etagSeenByBoth));
        var second = await Admin.SendAsync(Patch(code, new { status = "Inactive" }, etagSeenByBoth));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        await second.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");

        var current = await (await Admin.GetAsync($"/api/v1/ships/{code}")).ShouldBeOkJsonAsync();
        current.GetProperty("shipName").GetString().Should().Be("First writer");
        current.GetProperty("status").GetString().Should().Be("Active", "the stale update must not have been applied");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("*")]
    public async Task Update_Without_A_Precondition_Still_Works(string? ifMatch)
    {
        var code = await CreateShipAsync();
        (await Admin.SendAsync(Patch(code, new { shipName = "No precondition" }, ifMatch))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Malformed_If_Match_Is_A_Validation_Error_And_Weak_ETags_Never_Match()
    {
        var code = await CreateShipAsync();
        var etag = (await Admin.GetAsync($"/api/v1/ships/{code}")).Headers.ETag!.Tag;

        await (await Admin.SendAsync(Patch(code, new { shipName = "x" }, "no-quotes"))).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        await (await Admin.SendAsync(Patch(code, new { shipName = "x" }, "W/" + etag))).ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
    }

    [Fact]
    public async Task Ship_Lists_Expose_Each_Version_So_Clients_Can_Update_Without_A_Second_Read()
    {
        var page = await (await Admin.GetAsync("/api/v1/ships?pageSize=5")).ShouldBeOkJsonAsync();
        page.GetProperty("items").EnumerateArray().Should().OnlyContain(s => s.GetProperty("version").GetString()!.Length == 16);
    }

    [Fact]
    public async Task Stale_Version_Is_Rejected_By_The_Procedure_Itself()
    {
        var code = await CreateShipAsync();
        var adminId = await db.UserIdAsync(TestData.AdminEmail);

        var act = () => new ProcedureClient(db.AppConnectionString).ExecuteAsync("app.usp_Ship_Update", new
        {
            ShipCode = code,
            ShipName = "Stale",
            RequestedByUserId = adminId,
            ExpectedRowVersion = new byte[8],
        });

        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(50006);
    }

    [Fact]
    public async Task Database_Outage_Returns_503_And_The_Circuit_Opens_To_Fail_Fast()
    {
        var unreachable = new SqlConnectionStringBuilder(db.AppConnectionString) { Password = "wrong-password" };
        await using var api = new ApiFactory(db, new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = unreachable.ConnectionString,
            ["Database:Resilience:MinimumThroughput"] = "2",
            ["Database:Resilience:BreakDurationSeconds"] = "60",
        });
        var client = api.CreateClient(TestData.CrewingKey);

        // Each request fails at authentication (the key lookup cannot reach the database): 503, never 500.
        var first = await client.GetAsync("/api/v1/users/me");
        await first.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE");
        first.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(5));
        await client.GetAsync("/api/v1/users/me");

        // The circuit is now open: the database is not contacted and the client is told to wait out the break.
        var open = await client.GetAsync("/api/v1/users/me");
        await open.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE");
        open.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.FromSeconds(30));

        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
