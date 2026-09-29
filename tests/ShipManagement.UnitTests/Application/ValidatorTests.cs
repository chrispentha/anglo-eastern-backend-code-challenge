using FluentAssertions;
using ShipManagement.Application.ApiKeys;
using ShipManagement.Application.Crew;
using ShipManagement.Application.FinancialReports;
using ShipManagement.Application.Ships;
using ShipManagement.Application.Users;

namespace ShipManagement.UnitTests.Application;

public class CrewListQueryValidatorTests
{
    private readonly CrewListQueryValidator _validator = new();

    [Fact]
    public void Empty_Query_Is_Valid_And_Uses_Defaults()
    {
        _validator.Validate(new CrewListQuery()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(-1, false)]
    public void PageSize_Must_Be_1_To_100(int pageSize, bool valid)
    {
        _validator.Validate(new CrewListQuery { PageSize = pageSize }).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1_000_000, true)]
    [InlineData(0, false)]
    [InlineData(1_000_001, false)]
    public void PageNumber_Must_Be_Positive_And_Bounded(int pageNumber, bool valid)
    {
        _validator.Validate(new CrewListQuery { PageNumber = pageNumber }).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Search_Of_100_Characters_Is_Valid_But_101_Is_Not()
    {
        _validator.Validate(new CrewListQuery { Search = new string('a', 100) }).IsValid.Should().BeTrue();
        _validator.Validate(new CrewListQuery { Search = new string('a', 101) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Search_Length_Is_Measured_After_Trimming()
    {
        _validator.Validate(new CrewListQuery { Search = "  " + new string('a', 100) + "  " }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("salary")]
    [InlineData("LastName; DROP TABLE dbo.Ship;--")]
    public void SortBy_Outside_Whitelist_Is_Invalid(string sortBy)
    {
        var result = _validator.Validate(new CrewListQuery { SortBy = sortBy });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrewListQuery.SortBy));
    }

    [Fact]
    public void Every_Failing_Field_Is_Reported_At_Once()
    {
        var result = _validator.Validate(new CrewListQuery { PageNumber = 0, PageSize = 0, SortBy = "x", SortDirection = "x" });
        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            [nameof(CrewListQuery.PageNumber), nameof(CrewListQuery.PageSize), nameof(CrewListQuery.SortBy), nameof(CrewListQuery.SortDirection)]);
    }
}

public class FinancialReportQueryValidatorTests
{
    private readonly FinancialReportQueryValidator _validator = new();

    [Theory]
    [InlineData("2025-07", true)]
    [InlineData(" 2025-07 ", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("2025-13", false)]
    [InlineData("2025-7", false)]
    [InlineData("July 2025", false)]
    public void Period_Must_Be_yyyy_MM(string? period, bool valid)
    {
        _validator.Validate(new FinancialReportQuery { Period = period }).IsValid.Should().Be(valid);
    }
}

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        _validator.Validate(new CreateUserRequest { FullName = "Jane Doe", Email = "jane@example.com", Role = "Accountant" })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Email_Is_Optional()
    {
        _validator.Validate(new CreateUserRequest { FullName = "Jane Doe", Role = "Accountant" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void FullName_Is_Required(string? fullName)
    {
        _validator.Validate(new CreateUserRequest { FullName = fullName, Role = "Accountant" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void FullName_Longer_Than_100_Is_Rejected()
    {
        _validator.Validate(new CreateUserRequest { FullName = new string('x', 101), Role = "Accountant" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_Email_And_Missing_Role_Are_Both_Reported()
    {
        var result = _validator.Validate(new CreateUserRequest { FullName = "Jane", Email = "not-an-email" });
        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            [nameof(CreateUserRequest.Email), nameof(CreateUserRequest.Role)]);
    }
}

public class ShipRequestValidatorTests
{
    [Fact]
    public void Valid_Create_Request_Passes()
    {
        new CreateShipRequestValidator()
            .Validate(new CreateShipRequest { ShipCode = "ship99", ShipName = "Nautilus", FiscalYearCode = "0403" })
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("SH", "Name", "0112")]
    [InlineData("SHIP99", "", "0112")]
    [InlineData("SHIP99", "Name", "413")]
    [InlineData("SHIP99", "Name", "04-3")]
    public void Invalid_Create_Request_Fails(string code, string name, string fiscalYear)
    {
        new CreateShipRequestValidator()
            .Validate(new CreateShipRequest { ShipCode = code, ShipName = name, FiscalYearCode = fiscalYear })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Status_Must_Be_Letters_Only()
    {
        new CreateShipRequestValidator()
            .Validate(new CreateShipRequest { ShipCode = "SHIP99", ShipName = "N", FiscalYearCode = "0112", Status = "Active;--" })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_Requires_At_Least_One_Field()
    {
        new UpdateShipRequestValidator().Validate(new UpdateShipRequest()).IsValid.Should().BeFalse();
        new UpdateShipRequestValidator().Validate(new UpdateShipRequest { Status = "Inactive" }).IsValid.Should().BeTrue();
        new UpdateShipRequestValidator().Validate(new UpdateShipRequest { ShipName = "  " }).IsValid.Should().BeFalse();
    }
}

public class CreateApiKeyRequestValidatorTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(365, true)]
    [InlineData(0, false)]
    [InlineData(366, false)]
    public void ExpiresInDays_Must_Be_1_To_365(int? days, bool valid)
    {
        new CreateApiKeyRequestValidator().Validate(new CreateApiKeyRequest { ExpiresInDays = days }).IsValid.Should().Be(valid);
    }
}
