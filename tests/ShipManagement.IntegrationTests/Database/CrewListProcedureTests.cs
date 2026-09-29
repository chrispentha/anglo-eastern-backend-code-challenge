using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Database;

/// <summary>app.usp_Crew_ListByShip, called as the least-privilege API login with @AsOfDate pinned to 2025-06-15.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class CrewListProcedureTests(DatabaseFixture db)
{
    private readonly ProcedureClient _client = new(db.AppConnectionString);

    private Task<int> AdminId() => db.UserIdAsync(TestData.AdminEmail);

    [Fact]
    public async Task CrewList_Returns_Only_Onboard_And_Relief_Due_Crew()
    {
        var (rows, total) = await _client.CrewListAsync("TST01", await AdminId());

        total.Should().Be(10);
        rows.Select(r => r.Status).Distinct().Should().BeEquivalentTo(["Onboard", "Relief Due"]);
        rows.Select(r => r.CrewMemberId).Should().NotContain(["TC004", "TC005", "TC006"]);
    }

    [Fact]
    public async Task CrewStatus_Is_Onboard_When_Exactly_30_Days_Past_EOC()
    {
        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId());
        rows.Single(r => r.CrewMemberId == "TC001").Status.Should().Be("Onboard");
    }

    [Fact]
    public async Task CrewStatus_Is_ReliefDue_When_31_Days_Past_EOC()
    {
        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId());
        rows.Single(r => r.CrewMemberId == "TC002").Status.Should().Be("Relief Due");
        rows.Single(r => r.CrewMemberId == "TC013").Status.Should().Be("Relief Due");
    }

    [Fact]
    public async Task CrewStatus_Is_Onboard_On_The_Sign_On_Day_And_Planned_The_Day_Before()
    {
        var (today, _) = await _client.CrewListAsync("TST01", await AdminId());
        today.Should().Contain(r => r.CrewMemberId == "TC003" && r.Status == "Onboard");

        var (dayBefore, _) = await _client.CrewListAsync("TST01", await AdminId(), asOfDate: TestData.AsOfDate.AddDays(-1));
        dayBefore.Should().NotContain(r => r.CrewMemberId == "TC003");
    }

    [Fact]
    public async Task SignedOff_Crew_Is_Excluded_Even_When_The_Sign_Off_Date_Is_In_The_Future()
    {
        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId());
        rows.Should().NotContain(r => r.CrewMemberId == "TC005" || r.CrewMemberId == "TC006");
    }

    [Fact]
    public async Task Planned_Crew_Is_Listed_Once_Their_Sign_On_Day_Arrives()
    {
        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId(), asOfDate: TestData.AsOfDate.AddDays(1));
        rows.Should().Contain(r => r.CrewMemberId == "TC004" && r.Status == "Onboard");
    }

    [Fact]
    public async Task Age_Is_Full_Years_And_Changes_On_The_Birthday()
    {
        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId());

        rows.Single(r => r.CrewMemberId == "TC007").Age.Should().Be(35); // birthday today
        rows.Single(r => r.CrewMemberId == "TC008").Age.Should().Be(34); // birthday tomorrow
        rows.Single(r => r.CrewMemberId == "TC009").Age.Should().Be(25); // born 29 Feb 2000
    }

    [Fact]
    public async Task SignOnDate_Is_Labelled_dd_MMM_yyyy()
    {
        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId());
        rows.Single(r => r.CrewMemberId == "TC007").SignOnDateLabel.Should().Be("05 Apr 2025");
    }

    [Fact]
    public async Task Rank_Sort_Uses_Seniority_Then_Crew_Id()
    {
        var (asc, _) = await _client.CrewListAsync("TST01", await AdminId());
        asc.Select(r => r.CrewMemberId).Should().Equal(TestData.Tst01CrewBySeniority);

        var (desc, _) = await _client.CrewListAsync("TST01", await AdminId(), sortDirection: "desc");
        desc.First().CrewMemberId.Should().Be("TC010");  // Wiper
        desc.Last().CrewMemberId.Should().Be("TC001");   // Master
    }

    [Theory]
    [InlineData("crewMemberId")]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("age")]
    [InlineData("nationality")]
    [InlineData("signOnDate")]
    [InlineData("status")]
    public async Task Every_Sort_Key_Orders_Rows_In_Both_Directions(string sortBy)
    {
        var (asc, _) = await _client.CrewListAsync("TST01", await AdminId(), sortBy: sortBy, sortDirection: "asc");
        var (desc, _) = await _client.CrewListAsync("TST01", await AdminId(), sortBy: sortBy, sortDirection: "desc");

        Expression<Func<CrewRow, object>> key = sortBy switch
        {
            "crewMemberId" => r => r.CrewMemberId,
            "firstName" => r => r.FirstName,
            "lastName" => r => r.LastName,
            "age" => r => r.Age,
            "nationality" => r => r.Nationality,
            "signOnDate" => r => r.SignOnDate,
            _ => r => r.Status,
        };
        asc.Should().BeInAscendingOrder(key, StringOrderComparer.Instance);
        desc.Should().BeInDescendingOrder(key, StringOrderComparer.Instance);
    }

    [Fact]
    public async Task Paging_Through_Every_Page_Returns_Each_Crew_Member_Exactly_Once()
    {
        var seen = new List<string>();
        for (var page = 1; page <= 4; page++)
        {
            var (rows, total) = await _client.CrewListAsync("TST01", await AdminId(), pageNumber: page, pageSize: 3);
            total.Should().Be(10);
            seen.AddRange(rows.Select(r => r.CrewMemberId));
        }

        seen.Should().Equal(TestData.Tst01CrewBySeniority);
    }

    [Fact]
    public async Task Page_Beyond_The_End_Is_Empty_But_Reports_The_Total()
    {
        var (rows, total) = await _client.CrewListAsync("TST01", await AdminId(), pageNumber: 50, pageSize: 10);
        rows.Should().BeEmpty();
        total.Should().Be(10);
    }

    [Theory]
    [InlineData("05 Apr", new[] { "TC007", "TC008" })]
    [InlineData("05 apr 2025", new[] { "TC007", "TC008" })]
    [InlineData("phil", new[] { "TC001", "TC002" })]
    [InlineData("PHILIPPOU", new[] { "TC002" })]
    [InlineData("Burmese", new[] { "TC013", "TC012" })]
    [InlineData("Able Seaman", new[] { "TC007", "TC008" })]
    [InlineData("TC01", new[] { "TC011", "TC013", "TC012", "TC010" })]
    [InlineData("35", new[] { "TC007" })]
    [InlineData("γιώργος", new[] { "TC010" })]
    public async Task Search_Matches_Any_Column_Case_Insensitively(string search, string[] expected)
    {
        var (rows, total) = await _client.CrewListAsync("TST01", await AdminId(), search: search);
        rows.Select(r => r.CrewMemberId).Should().BeEquivalentTo(expected);
        total.Should().Be(expected.Length);
    }

    [Fact]
    public async Task Search_Never_Filters_By_Status()
    {
        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId(), search: "Relief");
        rows.Should().BeEmpty();
    }

    [Theory]
    [InlineData("%", new string[0])]
    [InlineData("[a-z]", new string[0])]
    [InlineData("_", new[] { "TC011" })]
    [InlineData("r_s", new[] { "TC011" })]
    public async Task Search_Treats_Wildcard_Characters_Literally(string search, string[] expected)
    {
        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId(), search: search);
        rows.Select(r => r.CrewMemberId).Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("' OR 1=1--")]
    [InlineData("'; DROP TABLE dbo.Ship;--")]
    [InlineData("\"; EXEC xp_cmdshell 'dir';--")]
    public async Task Search_Injection_Payloads_Match_Nothing_And_Change_Nothing(string payload)
    {
        var shipsBefore = await db.QueryAdminScalarAsync<int>("SELECT COUNT(*) FROM dbo.Ship");

        var (rows, _) = await _client.CrewListAsync("TST01", await AdminId(), search: payload);

        rows.Should().BeEmpty();
        (await db.QueryAdminScalarAsync<int>("SELECT COUNT(*) FROM dbo.Ship")).Should().Be(shipsBefore);
    }

    [Theory]
    [InlineData("rank; DROP TABLE dbo.Ship;--", "asc")]
    [InlineData("salary", "asc")]
    [InlineData("rank", "up")]
    public async Task Sort_Parameters_Outside_The_Whitelist_Are_Rejected(string sortBy, string direction)
    {
        var adminId = await AdminId();
        var act = () => _client.CrewListAsync("TST01", adminId, sortBy: sortBy, sortDirection: direction);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(50001);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Paging_Parameters_Out_Of_Range_Are_Rejected(int pageNumber, int pageSize)
    {
        var adminId = await AdminId();
        var act = () => _client.CrewListAsync("TST01", adminId, pageNumber: pageNumber, pageSize: pageSize);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(50001);
    }

    [Fact]
    public async Task Inactive_Ship_Raises_Ship_Inactive()
    {
        var adminId = await AdminId();
        var act = () => _client.CrewListAsync("TST03", adminId);
        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().StartWith("SHIP_INACTIVE|");
    }

    [Fact]
    public async Task Unknown_And_Unassigned_Ships_Raise_The_Same_Not_Found_Error()
    {
        var other = await db.UserIdAsync(TestData.OtherEmail);

        var unknown = () => _client.CrewListAsync("NOSHIP", other);
        var unassigned = () => _client.CrewListAsync("TST01", other);

        (await unknown.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(50002);
        (await unassigned.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(50002);
    }

    [Fact]
    public async Task Assigned_Non_Administrator_Can_List_Crew()
    {
        var (rows, _) = await _client.CrewListAsync("tst01", await db.UserIdAsync(TestData.CrewingEmail));
        rows.Should().HaveCount(10);
    }

    /// <summary>Orders strings with the database's case-insensitive collation semantics.</summary>
    private sealed class StringOrderComparer : IComparer<object>
    {
        public static readonly StringOrderComparer Instance = new();

        public int Compare(object? x, object? y) => (x, y) switch
        {
            (string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase),
            (IComparable a, _) => a.CompareTo(y),
            _ => 0,
        };
    }
}
