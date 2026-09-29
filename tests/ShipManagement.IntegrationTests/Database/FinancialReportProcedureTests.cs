using FluentAssertions;
using Microsoft.Data.SqlClient;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Database;

/// <summary>
/// app.usp_FinancialReport_Detail / _Summary against the TST01 (0112) and TST02 (0403) fixtures.
/// Expected numbers are derived from the business rules, not from the brief's example table.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class FinancialReportProcedureTests(DatabaseFixture db)
{
    private const string Detail = "app.usp_FinancialReport_Detail";
    private const string Summary = "app.usp_FinancialReport_Summary";
    private static readonly DateTime Feb2025 = new(2025, 2, 1);

    private readonly ProcedureClient _client = new(db.AppConnectionString);

    private async Task<List<ReportRow>> DetailAsync(string ship, DateTime period) =>
        (await _client.ReportAsync(Detail, ship, period, await db.UserIdAsync(TestData.AdminEmail))).Rows;

    [Fact]
    public async Task Ytd_For_April_To_March_Fiscal_Year_Crosses_The_Calendar_Year()
    {
        var (_, ytdStart, ytdEnd, fiscalYear) =
            await _client.ReportAsync(Detail, "TST02", Feb2025, await db.UserIdAsync(TestData.AdminEmail));

        fiscalYear.Should().Be("0403");
        ytdStart.Should().Be(new DateTime(2024, 4, 1));
        ytdEnd.Should().Be(Feb2025);
    }

    [Fact]
    public async Task Ytd_Is_A_Single_Month_In_The_First_Month_Of_The_Fiscal_Year()
    {
        var (rows, ytdStart, _, _) =
            await _client.ReportAsync(Detail, "TST02", new DateTime(2025, 4, 1), await db.UserIdAsync(TestData.AdminEmail));

        ytdStart.Should().Be(new DateTime(2025, 4, 1));
        var line = rows.Single(r => r.AccountNumber == "7110000");
        line.Budget.Should().Be(50);
        line.BudgetYtd.Should().Be(50);
        line.Actual.Should().BeNull();
        line.ActualYtd.Should().BeNull();
    }

    [Fact]
    public async Task Ytd_For_January_To_December_Fiscal_Year_Starts_In_January()
    {
        var (rows, ytdStart, _, _) =
            await _client.ReportAsync(Detail, "TST01", new DateTime(2025, 7, 1), await db.UserIdAsync(TestData.AdminEmail));

        ytdStart.Should().Be(new DateTime(2025, 1, 1));
        var line = rows.Single(r => r.AccountNumber == "7110000");
        line.BudgetYtd.Should().Be(700);   // Jan..Jul 2025; Dec 2024 belongs to the previous fiscal year
        line.Actual.Should().Be(80);
        line.Budget.Should().Be(100);
        line.Variance.Should().Be(-20);
    }

    [Fact]
    public async Task Child_Account_Sums_Transactions_And_Ignores_Other_Fiscal_Years()
    {
        var line = (await DetailAsync("TST02", Feb2025)).Single(r => r.AccountNumber == "7110000");

        line.Actual.Should().Be(300);       // 250 + 50
        line.Budget.Should().Be(300);
        line.Variance.Should().Be(0);
        line.ActualYtd.Should().Be(300);    // 2024-03 (previous FY) and 2025-03 (after period) excluded
        line.BudgetYtd.Should().Be(600);    // 100 + 200 + 300
        line.VarianceYtd.Should().Be(-300);
    }

    [Fact]
    public async Task Multiple_Budget_Lines_And_Transactions_In_A_Period_Are_Summed()
    {
        var line = (await DetailAsync("TST02", Feb2025)).Single(r => r.AccountNumber == "7135000");

        line.ActualYtd.Should().Be(1000);   // 300 + 0 + 700
        line.BudgetYtd.Should().Be(1000);   // 0 + 1000
        line.VarianceYtd.Should().Be(0);
    }

    [Fact]
    public async Task Zero_Is_Zero_And_Missing_Data_Is_Null()
    {
        var line = (await DetailAsync("TST02", Feb2025)).Single(r => r.AccountNumber == "7135000");

        line.Actual.Should().Be(0);          // a zero transaction exists in the period
        line.Budget.Should().BeNull();       // no budget rows in the period
        line.Variance.Should().Be(0);        // 0 - (no data)
    }

    [Fact]
    public async Task Account_With_Actuals_But_No_Budget_Has_Null_Budget_And_Variance_Equal_To_Actual()
    {
        var line = (await DetailAsync("TST02", Feb2025)).Single(r => r.AccountNumber == "7140000");

        line.Actual.Should().Be(400);
        line.Budget.Should().BeNull();
        line.Variance.Should().Be(400);
        line.BudgetYtd.Should().BeNull();
        line.VarianceYtd.Should().Be(400);
    }

    [Fact]
    public async Task Account_With_Budget_But_No_Actuals_Has_Null_Actual()
    {
        var line = (await DetailAsync("TST02", Feb2025)).Single(r => r.AccountNumber == "7210000");

        line.Actual.Should().BeNull();
        line.Budget.Should().Be(5000);
        line.Variance.Should().Be(-5000);
    }

    [Fact]
    public async Task Accounts_With_Only_Zero_Values_Are_Excluded()
    {
        var rows = await DetailAsync("TST02", Feb2025);
        rows.Should().NotContain(r => r.AccountNumber == "7120000");
        rows.Should().NotContain(r => r.AccountNumber == "7300000"); // no data at all under this parent
    }

    [Fact]
    public async Task Parents_Aggregate_All_Descendants_At_Every_Level()
    {
        var rows = await DetailAsync("TST02", Feb2025);

        var grants = rows.Single(r => r.AccountNumber == "7100000");
        grants.Actual.Should().Be(700);        // 300 + 0 + 0 + 400
        grants.Budget.Should().Be(300);        // 300 + 0 (7120000 zero row); 7135000 and 7140000 have none
        grants.ActualYtd.Should().Be(1700);    // 300 + 0 + 1000 + 400
        grants.BudgetYtd.Should().Be(1600);    // 600 + 0 + 1000

        var crew = rows.Single(r => r.AccountNumber == "7200000");
        crew.Actual.Should().BeNull();
        crew.Budget.Should().Be(5000);

        var root = rows.Single(r => r.AccountNumber == "7000000");
        root.HierarchyLevel.Should().Be(1);
        root.Actual.Should().Be(700);
        root.Budget.Should().Be(5300);
        root.Variance.Should().Be(-4600);
        root.ActualYtd.Should().Be(1700);
        root.BudgetYtd.Should().Be(6600);
        root.VarianceYtd.Should().Be(-4900);
    }

    [Fact]
    public async Task Detail_Is_In_Tree_Order_With_Parents_Before_Children_And_Total_Last()
    {
        var rows = await DetailAsync("TST02", Feb2025);

        rows.Select(r => r.AccountNumber).Should().Equal(
            "7000000", "7100000", "7110000", "7135000", "7140000", "7200000", "7210000", null);
        rows.Last().IsTotal.Should().BeTrue();
        rows.Last().Description.Should().Be("GRAND TOTAL");
    }

    [Fact]
    public async Task Summary_Contains_Parent_Accounts_Only_And_Ties_To_Detail()
    {
        var adminId = await db.UserIdAsync(TestData.AdminEmail);
        var detail = (await _client.ReportAsync(Detail, "TST02", Feb2025, adminId)).Rows;
        var summary = (await _client.ReportAsync(Summary, "TST02", Feb2025, adminId)).Rows;

        summary.Where(r => !r.IsTotal).Should().OnlyContain(r => r.AccountType == "P");
        summary.Select(r => r.AccountNumber).Should().Equal("7000000", "7100000", "7200000", null);

        foreach (var line in summary)
        {
            detail.Single(d => d.AccountNumber == line.AccountNumber && d.IsTotal == line.IsTotal)
                .Should().BeEquivalentTo(line);
        }
    }

    [Fact]
    public async Task Grand_Total_Equals_The_Root_Accounts()
    {
        var rows = await DetailAsync("TST02", Feb2025);
        var total = rows.Single(r => r.IsTotal);
        var root = rows.Single(r => r.AccountNumber == "7000000");

        total.Should().BeEquivalentTo(root, options => options
            .Including(r => r.Actual).Including(r => r.Budget).Including(r => r.Variance)
            .Including(r => r.ActualYtd).Including(r => r.BudgetYtd).Including(r => r.VarianceYtd));
    }

    [Fact]
    public async Task Period_With_No_Data_Returns_No_Lines()
    {
        (await DetailAsync("TST02", new DateTime(2023, 1, 1))).Should().BeEmpty();
    }

    [Fact]
    public async Task Inactive_Ship_Raises_Ship_Inactive()
    {
        var adminId = await db.UserIdAsync(TestData.AdminEmail);
        var act = () => _client.ReportAsync(Detail, "TST03", Feb2025, adminId);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(50004);
    }

    [Fact]
    public async Task Unassigned_Ship_Raises_Not_Found()
    {
        var otherId = await db.UserIdAsync(TestData.OtherEmail);
        var act = () => _client.ReportAsync(Summary, "TST02", Feb2025, otherId);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(50002);
    }

    [Fact]
    public async Task Period_That_Is_Not_The_First_Of_A_Month_Is_Rejected()
    {
        var adminId = await db.UserIdAsync(TestData.AdminEmail);
        var act = () => _client.ReportAsync(Detail, "TST02", new DateTime(2025, 2, 15), adminId);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(50001);
    }

    [Fact]
    public async Task Sample_Data_From_The_Brief_Reports_300_Plus_0_Plus_700_For_Ship01_January_2025()
    {
        var line = (await DetailAsync("SHIP01", new DateTime(2025, 1, 1))).Single(r => r.AccountNumber == "7135000");

        line.Actual.Should().Be(1000);
        line.Budget.Should().Be(1000);
        line.Variance.Should().Be(0);
    }
}
