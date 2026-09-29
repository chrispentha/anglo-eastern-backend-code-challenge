using System.Net;
using System.Text.Json;
using FluentAssertions;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Api;

/// <summary>US-07..US-09 through the real HTTP pipeline ("today" is the real UTC date here, so no status assertions).</summary>
[Collection(IntegrationCollection.Name)]
public sealed class CrewAndReportsApiTests(DatabaseFixture db)
{
    private HttpClient Crewing => db.Api.CreateClient(TestData.CrewingKey);

    [Fact]
    public async Task Crew_List_Returns_A_Paged_Envelope()
    {
        var page = await (await Crewing.GetAsync("/api/v1/ships/tst01/crew?pageSize=3&sortBy=lastName&sortDirection=desc"))
            .ShouldBeOkJsonAsync();

        page.GetProperty("pageNumber").GetInt32().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().Be(3);
        page.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(3);
        var total = page.GetProperty("totalCount").GetInt32();
        page.GetProperty("totalPages").GetInt32().Should().Be((int)Math.Ceiling(total / 3.0));

        var first = page.GetProperty("items")[0];
        foreach (var field in new[] { "rankName", "crewMemberId", "firstName", "lastName", "age", "nationality", "signOnDate", "signOnDateLabel", "status" })
        {
            first.TryGetProperty(field, out _).Should().BeTrue(field);
        }

        first.TryGetProperty("birthDate", out _).Should().BeFalse("only age is exposed (SEC-08)");
    }

    [Fact]
    public async Task Crew_List_For_Sample_Ship_Has_At_Least_20_Crew()
    {
        var page = await (await db.Api.CreateClient(TestData.AdminKey).GetAsync("/api/v1/ships/SHIP01/crew?pageSize=100")).ShouldBeOkJsonAsync();

        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(20);
        page.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("status").GetString())
            .Should().OnlyContain(s => s == "Onboard" || s == "Relief Due");
    }

    [Theory]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageNumber=0", "pageNumber")]
    [InlineData("sortBy=salary", "sortBy")]
    [InlineData("sortDirection=up", "sortDirection")]
    [InlineData("pageSize=ten", "pageSize")]
    public async Task Invalid_Crew_Query_Names_The_Field(string query, string field)
    {
        var problem = await (await Crewing.GetAsync($"/api/v1/ships/TST01/crew?{query}"))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        problem.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Search_Longer_Than_100_Characters_Is_Rejected()
    {
        await (await Crewing.GetAsync($"/api/v1/ships/TST01/crew?search={new string('a', 101)}"))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Theory]
    [InlineData("%27%20OR%201%3D1--")]
    [InlineData("%25")]
    [InlineData("%27%3B%20DROP%20TABLE%20dbo.Ship%3B--")]
    public async Task Injection_Attempts_In_Search_Return_No_Rows(string encodedSearch)
    {
        var page = await (await Crewing.GetAsync($"/api/v1/ships/TST01/crew?search={encodedSearch}")).ShouldBeOkJsonAsync();
        page.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Crew_List_Of_Inactive_Ship_Is_A_Conflict()
    {
        await (await Crewing.GetAsync("/api/v1/ships/TST03/crew")).ShouldBeProblemAsync(HttpStatusCode.Conflict, "SHIP_INACTIVE");
    }

    [Fact]
    public async Task Detail_Report_Exposes_The_Fiscal_Ytd_Window_And_Keeps_Nulls()
    {
        var report = await (await Crewing.GetAsync("/api/v1/ships/TST02/financial-reports/detail?period=2025-02")).ShouldBeOkJsonAsync();

        report.GetProperty("shipCode").GetString().Should().Be("TST02");
        report.GetProperty("reportType").GetString().Should().Be("Detail");
        report.GetProperty("fiscalYearCode").GetString().Should().Be("0403");
        report.GetProperty("ytdStart").GetString().Should().Be("2024-04");
        report.GetProperty("ytdEnd").GetString().Should().Be("2025-02");
        report.GetProperty("columns").GetProperty("actualYtd").GetString().Should().Be("Actual YTD (Apr 2024 - Feb 2025)");

        var lines = report.GetProperty("lines").EnumerateArray().ToList();
        var cadetGrants = lines.Single(l => l.GetProperty("accountNumber").ValueKind == JsonValueKind.String
                                           && l.GetProperty("accountNumber").GetString() == "7140000");
        cadetGrants.GetProperty("budget").ValueKind.Should().Be(JsonValueKind.Null);
        cadetGrants.GetProperty("actual").GetDecimal().Should().Be(400);
        lines.Last().GetProperty("isTotal").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Summary_Report_Contains_Parents_And_Total_Only()
    {
        var report = await (await Crewing.GetAsync("/api/v1/ships/TST02/financial-reports/summary?period=2025-02")).ShouldBeOkJsonAsync();

        report.GetProperty("lines").EnumerateArray()
            .Where(l => !l.GetProperty("isTotal").GetBoolean())
            .Should().OnlyContain(l => l.GetProperty("accountType").GetString() == "P");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?period=")]
    [InlineData("?period=2025-13")]
    [InlineData("?period=2025-7")]
    [InlineData("?period=Feb-2025")]
    public async Task Report_Requires_A_Valid_Period(string query)
    {
        var problem = await (await Crewing.GetAsync($"/api/v1/ships/TST02/financial-reports/detail{query}"))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        problem.GetProperty("errors").TryGetProperty("period", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Report_Of_Unassigned_Ship_Is_Not_Found()
    {
        await (await db.Api.CreateClient(TestData.OtherKey).GetAsync("/api/v1/ships/TST02/financial-reports/detail?period=2025-02"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "SHIP_NOT_FOUND");
    }

    [Fact]
    public async Task Report_Of_Inactive_Ship_Is_A_Conflict()
    {
        await (await Crewing.GetAsync("/api/v1/ships/TST03/financial-reports/detail?period=2025-02"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "SHIP_INACTIVE");
    }
}
