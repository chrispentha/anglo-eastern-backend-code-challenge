using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShipManagement.Application.FinancialReports;
using ShipManagement.Domain.Errors;

namespace ShipManagement.UnitTests.Application;

public class FinancialReportServiceTests
{
    private readonly IFinancialReportRepository _repository = Substitute.For<IFinancialReportRepository>();

    private FinancialReportService CreateService(FakeCurrentUser user) =>
        new(_repository, user, new FinancialReportQueryValidator(), NullLogger<FinancialReportService>.Instance);

    [Fact]
    public async Task Report_Labels_Use_The_Fiscal_Ytd_Window_Returned_By_The_Database()
    {
        var line = new FinancialReportLineDto("7110000", "PERFORMANCE BONUSES", "C", 3, "7100000",
            Actual: null, Budget: 300m, Variance: -300m, ActualYtd: 0m, BudgetYtd: 600m, VarianceYtd: -600m, IsTotal: false);
        _repository.GetAsync("SHIP02", new DateOnly(2025, 2, 1), FinancialReportType.Detail, 42, Arg.Any<CancellationToken>())
            .Returns(new FinancialReportData("0403", new DateOnly(2024, 4, 1), new DateOnly(2025, 2, 1), [line]));

        var report = await CreateService(FakeCurrentUser.AssignedTo("SHIP02"))
            .GetAsync("ship02", FinancialReportType.Detail, new FinancialReportQuery { Period = "2025-02" }, CancellationToken.None);

        report.ShipCode.Should().Be("SHIP02");
        report.ReportType.Should().Be("Detail");
        report.FiscalYearCode.Should().Be("0403");
        report.Period.Should().Be("2025-02");
        report.YtdStart.Should().Be("2024-04");
        report.YtdEnd.Should().Be("2025-02");
        report.Columns.Actual.Should().Be("Actual (Feb 2025)");
        report.Columns.ActualYtd.Should().Be("Actual YTD (Apr 2024 - Feb 2025)");
        report.Columns.VarianceYtd.Should().Be("Variance YTD (Apr 2024 - Feb 2025)");
    }

    [Fact]
    public async Task Report_Preserves_Null_As_No_Data_And_Zero_As_Zero()
    {
        var line = new FinancialReportLineDto("7140000", "CADET TRAINING GRANTS", "C", 3, "7100000",
            Actual: 0m, Budget: null, Variance: 0m, ActualYtd: 400m, BudgetYtd: null, VarianceYtd: 400m, IsTotal: false);
        _repository.GetAsync(default!, default, default, default, default)
            .ReturnsForAnyArgs(new FinancialReportData("0112", new DateOnly(2025, 1, 1), new DateOnly(2025, 7, 1), [line]));

        var report = await CreateService(FakeCurrentUser.Admin())
            .GetAsync("SHIP01", FinancialReportType.Summary, new FinancialReportQuery { Period = "2025-07" }, CancellationToken.None);

        report.Lines.Should().ContainSingle().Which.Should().BeEquivalentTo(line);
        report.Lines[0].Actual.Should().Be(0m);
        report.Lines[0].Budget.Should().BeNull();
    }

    [Fact]
    public async Task Report_For_Unassigned_Ship_Is_Not_Found()
    {
        var act = () => CreateService(FakeCurrentUser.AssignedTo("SHIP01"))
            .GetAsync("SHIP03", FinancialReportType.Detail, new FinancialReportQuery { Period = "2025-07" }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _repository.DidNotReceiveWithAnyArgs().GetAsync(default!, default, default, default, default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2025-13")]
    [InlineData("2025/07")]
    public async Task Report_With_Invalid_Period_Is_A_Validation_Error(string? period)
    {
        var act = () => CreateService(FakeCurrentUser.Admin())
            .GetAsync("SHIP01", FinancialReportType.Detail, new FinancialReportQuery { Period = period }, CancellationToken.None);

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Errors.Should().ContainKey("period");
    }
}
