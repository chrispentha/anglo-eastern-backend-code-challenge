using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Api;

/// <summary>US-01..US-06 through the real HTTP pipeline.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class UsersAndShipsApiTests(DatabaseFixture db)
{
    private HttpClient Admin => db.Api.CreateClient(TestData.AdminKey);

    private static string UniqueCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    [Fact]
    public async Task Admin_Creates_A_User_And_Can_Read_It_Back()
    {
        var email = $"{Guid.NewGuid():N}@example.test";

        var response = await Admin.PostAsJsonAsync("/api/v1/users", new { fullName = "  New Superintendent ", email, role = "Superintendent" });
        var created = await response.ShouldBeOkJsonAsync(HttpStatusCode.Created);

        created.GetProperty("fullName").GetString().Should().Be("New Superintendent");
        created.GetProperty("isActive").GetBoolean().Should().BeTrue();
        response.Headers.Location.Should().NotBeNull();

        var fetched = await (await Admin.GetAsync(response.Headers.Location)).ShouldBeOkJsonAsync();
        fetched.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Duplicate_Email_Is_A_Conflict()
    {
        var response = await Admin.PostAsJsonAsync("/api/v1/users", new { fullName = "Dup", email = TestData.CrewingEmail, role = "Accountant" });
        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "USER_EMAIL_CONFLICT");
    }

    [Fact]
    public async Task Invalid_User_Lists_Every_Failing_Field()
    {
        var response = await Admin.PostAsJsonAsync("/api/v1/users", new { fullName = "", email = "nope" });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("fullName", out _).Should().BeTrue();
        errors.TryGetProperty("email", out _).Should().BeTrue();
        errors.TryGetProperty("role", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_Role_Is_Rejected_By_The_Database_Rules()
    {
        var response = await Admin.PostAsJsonAsync("/api/v1/users", new { fullName = "X", role = "Captain" });
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Unknown_Json_Properties_Are_Rejected_To_Prevent_Mass_Assignment()
    {
        var response = await Admin.PostAsJsonAsync("/api/v1/users", new { fullName = "X", role = "Accountant", isActive = false, userId = 1 });
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Malformed_Json_Is_A_Validation_Error()
    {
        var response = await Admin.PostAsync("/api/v1/users", new StringContent("{ \"fullName\": ", Encoding.UTF8, "application/json"));
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Non_Json_Content_Type_Is_Unsupported()
    {
        var response = await Admin.PostAsync("/api/v1/users", new StringContent("fullName=x", Encoding.UTF8, "application/x-www-form-urlencoded"));
        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
    }

    [Fact]
    public async Task Users_Are_Listed_With_Paging_And_Role_Filter()
    {
        var page = await (await Admin.GetAsync("/api/v1/users?pageSize=2&role=CrewingOfficer")).ShouldBeOkJsonAsync();

        page.GetProperty("pageSize").GetInt32().Should().Be(2);
        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        page.GetProperty("items").EnumerateArray().Should().OnlyContain(u => u.GetProperty("role").GetString() == "CrewingOfficer");
    }

    [Theory]
    [InlineData("/api/v1/users?pageSize=0")]
    [InlineData("/api/v1/users?pageSize=101")]
    [InlineData("/api/v1/users?pageSize=abc")]
    [InlineData("/api/v1/users?sortDirection=sideways")]
    public async Task Invalid_Paging_Is_A_Validation_Error(string url)
    {
        await (await Admin.GetAsync(url)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Admin_Creates_A_Ship_With_Normalised_Code()
    {
        var code = UniqueCode("N");

        var response = await Admin.PostAsJsonAsync("/api/v1/ships", new { shipCode = code.ToLowerInvariant(), shipName = "Nautilus", fiscalYearCode = "0706" });
        var ship = await response.ShouldBeOkJsonAsync(HttpStatusCode.Created);

        ship.GetProperty("shipCode").GetString().Should().Be(code);
        ship.GetProperty("status").GetString().Should().Be("Active");
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/ships/{code}");
    }

    [Fact]
    public async Task Duplicate_Ship_Code_Is_A_Conflict_Naming_The_Code()
    {
        var response = await Admin.PostAsJsonAsync("/api/v1/ships", new { shipCode = "SHIP01", shipName = "Copy", fiscalYearCode = "0112" });
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "SHIP_CODE_CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("SHIP01");
    }

    [Fact]
    public async Task Unknown_Fiscal_Year_Code_Is_Invalid()
    {
        var response = await Admin.PostAsJsonAsync("/api/v1/ships", new { shipCode = UniqueCode("F"), shipName = "X", fiscalYearCode = "0413" });
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        problem.GetProperty("detail").GetString().Should().Contain("fiscalYearCode");
    }

    [Fact]
    public async Task Non_Admin_Sees_Only_Assigned_Ships()
    {
        var page = await (await db.Api.CreateClient(TestData.CrewingKey).GetAsync("/api/v1/ships?pageSize=100")).ShouldBeOkJsonAsync();

        page.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("shipCode").GetString())
            .Should().BeEquivalentTo(["TST01", "TST02", "TST03"]);
    }

    [Fact]
    public async Task Ship_List_Can_Be_Filtered_By_Status()
    {
        var page = await (await Admin.GetAsync("/api/v1/ships?status=Inactive&pageSize=100")).ShouldBeOkJsonAsync();
        page.GetProperty("items").EnumerateArray().Should().OnlyContain(s => s.GetProperty("status").GetString() == "Inactive");
        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(3); // SHIP04, SHIP05, TST03
    }

    [Fact]
    public async Task Assigned_User_And_Administrator_Can_Read_A_Ship_Including_An_Inactive_One()
    {
        var ship = await (await db.Api.CreateClient(TestData.CrewingKey).GetAsync("/api/v1/ships/tst03")).ShouldBeOkJsonAsync();
        ship.GetProperty("shipCode").GetString().Should().Be("TST03");
        ship.GetProperty("status").GetString().Should().Be("Inactive");
        ship.GetProperty("fiscalYearCode").GetString().Should().Be("0112");

        var sample = await (await Admin.GetAsync("/api/v1/ships/SHIP02")).ShouldBeOkJsonAsync();
        sample.GetProperty("shipName").GetString().Should().Be("Thousand Sunny");
    }

    [Fact]
    public async Task Unknown_And_Unassigned_Ships_Are_Indistinguishable()
    {
        var unassigned = await db.Api.CreateClient(TestData.CrewingKey).GetAsync("/api/v1/ships/SHIP01");
        var unknown = await db.Api.CreateClient(TestData.CrewingKey).GetAsync("/api/v1/ships/NOSUCH1");

        await unassigned.ShouldBeProblemAsync(HttpStatusCode.NotFound, "SHIP_NOT_FOUND");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "SHIP_NOT_FOUND");
    }

    [Fact]
    public async Task Invalid_Ship_Code_In_The_Route_Is_A_Validation_Error()
    {
        await (await Admin.GetAsync("/api/v1/ships/X%27--")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Assignment_Is_Idempotent_And_Takes_Effect_Immediately()
    {
        var otherId = await db.UserIdAsync(TestData.OtherEmail);
        var other = db.Api.CreateClient(TestData.OtherKey);

        await (await other.GetAsync("/api/v1/ships/TST01/crew")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "SHIP_NOT_FOUND");

        (await Admin.PutAsync($"/api/v1/users/{otherId}/ships/tst01", null)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Admin.PutAsync($"/api/v1/users/{otherId}/ships/TST01", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The cached security context was invalidated: access works on the very next request (D-25).
        (await other.GetAsync("/api/v1/ships/TST01/crew")).StatusCode.Should().Be(HttpStatusCode.OK);
        var mine = await (await other.GetAsync("/api/v1/users/me/ships")).ShouldBeOkJsonAsync();
        mine.EnumerateArray().Select(s => s.GetProperty("shipCode").GetString()).Should().Contain("TST01");

        (await Admin.DeleteAsync($"/api/v1/users/{otherId}/ships/TST01")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Admin.DeleteAsync($"/api/v1/users/{otherId}/ships/TST01")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await (await other.GetAsync("/api/v1/ships/TST01/crew")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "SHIP_NOT_FOUND");
    }

    [Fact]
    public async Task Assigning_Unknown_Ship_Or_User_Is_Not_Found()
    {
        var otherId = await db.UserIdAsync(TestData.OtherEmail);
        await (await Admin.PutAsync($"/api/v1/users/{otherId}/ships/NOSUCH1", null)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "SHIP_NOT_FOUND");
        await (await Admin.PutAsync("/api/v1/users/999999/ships/TST01", null)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "USER_NOT_FOUND");
    }

    [Fact]
    public async Task Deactivated_Ship_Disappears_From_Operational_Queries()
    {
        var code = UniqueCode("D");
        (await Admin.PostAsJsonAsync("/api/v1/ships", new { shipCode = code, shipName = "Short Lived", fiscalYearCode = "0112" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await Admin.GetAsync($"/api/v1/ships/{code}/crew")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await (await Admin.PatchAsJsonAsync($"/api/v1/ships/{code}", new { status = "Inactive" })).ShouldBeOkJsonAsync();
        updated.GetProperty("status").GetString().Should().Be("Inactive");

        await (await Admin.GetAsync($"/api/v1/ships/{code}/crew")).ShouldBeProblemAsync(HttpStatusCode.Conflict, "SHIP_INACTIVE");
        await (await Admin.GetAsync($"/api/v1/ships/{code}/financial-reports/summary?period=2025-01"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "SHIP_INACTIVE");
    }

    [Fact]
    public async Task Fiscal_Year_Cannot_Be_Changed_After_Creation()
    {
        var response = await Admin.PatchAsJsonAsync("/api/v1/ships/TST01", new { fiscalYearCode = "0403" });
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Writes_Are_Audited()
    {
        var code = UniqueCode("A");
        await Admin.PostAsJsonAsync("/api/v1/ships", new { shipCode = code, shipName = "Audited", fiscalYearCode = "0112" });

        var audited = await db.QueryAdminScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = 'SHIP_CREATED' AND EntityKey = @code", new { code });
        audited.Should().Be(1);
    }
}
