using FluentAssertions;
using Microsoft.Data.SqlClient;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Database;

/// <summary>Least privilege (SEC-05), declarative integrity rules (D-05, D-08, D-22) and audit immutability (SEC-09).</summary>
[Collection(IntegrationCollection.Name)]
public sealed class DatabaseSecurityAndIntegrityTests(DatabaseFixture db)
{
    private readonly ProcedureClient _app = new(db.AppConnectionString);

    [Theory]
    [InlineData("SELECT TOP (1) * FROM dbo.Ship")]
    [InlineData("SELECT TOP (1) * FROM dbo.CrewMember")]
    [InlineData("SELECT TOP (1) * FROM dbo.ApiKey")]
    [InlineData("UPDATE dbo.Ship SET ShipName = N'x'")]
    [InlineData("DELETE FROM dbo.AuditLog")]
    [InlineData("EXEC dbo.usp_FinancialReport_Build @ShipCode = 'SHIP01', @Period = '2025-01-01', @RequestedByUserId = 1, @ParentsOnly = 0")]
    public async Task App_Login_Cannot_Touch_Tables_Or_Internal_Procedures(string sql)
    {
        var act = () => _app.ExecuteTextAsync(sql);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(229); // permission denied
    }

    [Fact]
    public async Task App_Login_Can_Execute_Api_Procedures()
    {
        await _app.ExecuteAsync("app.usp_Health_Ping", new { });
    }

    [Fact]
    public async Task Posting_A_Budget_To_A_Parent_Account_Is_Impossible()
    {
        var act = () => db.ExecuteAdminAsync("""
            INSERT INTO dbo.BudgetEntry (ShipId, AccountId, AccountType, AccountPeriod, BudgetAmount)
            SELECT s.ShipId, a.AccountId, 'P', '2025-01-01', 10
            FROM dbo.Ship s CROSS JOIN dbo.Account a WHERE s.ShipCode = 'TST01' AND a.AccountNumber = '7100000'
            """);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);

        var actDefault = () => db.ExecuteAdminAsync("""
            INSERT INTO dbo.AccountTransaction (ShipId, AccountId, AccountPeriod, ActualAmount)
            SELECT s.ShipId, a.AccountId, '2025-01-01', 10
            FROM dbo.Ship s CROSS JOIN dbo.Account a WHERE s.ShipCode = 'TST01' AND a.AccountNumber = '7100000'
            """);
        (await actDefault.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
    }

    [Fact]
    public async Task Negative_Amounts_Are_Rejected()
    {
        var act = () => db.ExecuteAdminAsync("""
            INSERT INTO dbo.AccountTransaction (ShipId, AccountId, AccountPeriod, ActualAmount)
            SELECT s.ShipId, a.AccountId, '2025-01-01', -1
            FROM dbo.Ship s CROSS JOIN dbo.Account a WHERE s.ShipCode = 'TST01' AND a.AccountNumber = '7110000'
            """);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
    }

    [Fact]
    public async Task Accounting_Period_Must_Be_The_First_Day_Of_A_Month()
    {
        var act = () => db.ExecuteAdminAsync("""
            INSERT INTO dbo.BudgetEntry (ShipId, AccountId, AccountPeriod, BudgetAmount)
            SELECT s.ShipId, a.AccountId, '2025-01-15', 1
            FROM dbo.Ship s CROSS JOIN dbo.Account a WHERE s.ShipCode = 'TST01' AND a.AccountNumber = '7110000'
            """);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
    }

    [Fact]
    public async Task A_Child_Account_Cannot_Have_Children()
    {
        var act = () => db.ExecuteAdminAsync("""
            INSERT INTO dbo.Account (AccountNumber, Description, AccountType, ParentAccountId)
            SELECT '7110001', N'INVALID CHILD OF CHILD', 'C', AccountId FROM dbo.Account WHERE AccountNumber = '7110000'
            """);
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
    }

    [Fact]
    public async Task Invalid_Fiscal_Year_Code_Cannot_Exist()
    {
        var act = () => db.ExecuteAdminAsync("INSERT INTO dbo.FiscalYear VALUES ('0413', 4, 13, N'Invalid')");
        (await act.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
    }

    [Fact]
    public async Task Audit_Log_Rows_Cannot_Be_Changed_Even_By_An_Administrator_Login()
    {
        var update = () => db.ExecuteAdminAsync("UPDATE dbo.AuditLog SET Action = 'TAMPERED'");
        var delete = () => db.ExecuteAdminAsync("DELETE FROM dbo.AuditLog");

        (await update.Should().ThrowAsync<SqlException>()).Which.Message.Should().StartWith("AUDIT_IMMUTABLE|");
        (await delete.Should().ThrowAsync<SqlException>()).Which.Message.Should().StartWith("AUDIT_IMMUTABLE|");
    }

    [Fact]
    public async Task Deployment_Is_Rerunnable_Without_Duplicating_Sample_Data()
    {
        var shipsBefore = await db.QueryAdminScalarAsync<int>("SELECT COUNT(*) FROM dbo.Ship");
        var crewBefore = await db.QueryAdminScalarAsync<int>("SELECT COUNT(*) FROM dbo.CrewServiceHistory");

        // Re-runs every script, including the sample-data verification of every minimum in the brief.
        await db.RunDeployAsync();

        (await db.QueryAdminScalarAsync<int>("SELECT COUNT(*) FROM dbo.Ship")).Should().Be(shipsBefore);
        (await db.QueryAdminScalarAsync<int>("SELECT COUNT(*) FROM dbo.CrewServiceHistory")).Should().Be(crewBefore);
    }
}
