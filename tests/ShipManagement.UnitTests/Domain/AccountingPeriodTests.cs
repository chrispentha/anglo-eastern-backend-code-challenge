using FluentAssertions;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.UnitTests.Domain;

public class AccountingPeriodTests
{
    [Fact]
    public void Period_Parses_yyyy_MM_To_First_Day_Of_Month()
    {
        AccountingPeriod.TryParse("2025-07", out var period).Should().BeTrue();

        period.FirstDay.Should().Be(new DateOnly(2025, 7, 1));
        period.ToString().Should().Be("2025-07");
        period.ToLabel().Should().Be("Jul 2025");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2025-7")]
    [InlineData("2025-13")]
    [InlineData("2025-00")]
    [InlineData("07-2025")]
    [InlineData("2025/07")]
    [InlineData("2025-07-01")]
    [InlineData("1999-12")]
    [InlineData("2100-01")]
    [InlineData(" 2025-07")]
    [InlineData("abc")]
    public void Period_Is_Rejected_When_Not_Strict_yyyy_MM_In_Range(string? input)
    {
        AccountingPeriod.TryParse(input, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("2000-01")]
    [InlineData("2099-12")]
    public void Period_Accepts_Range_Boundaries(string input)
    {
        AccountingPeriod.TryParse(input, out _).Should().BeTrue();
    }

    [Fact]
    public void Parse_Throws_On_Invalid_Input()
    {
        var act = () => AccountingPeriod.Parse("2025-13");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void FromDate_Uses_Year_And_Month_Only()
    {
        AccountingPeriod.FromDate(new DateOnly(2024, 4, 1)).ToLabel().Should().Be("Apr 2024");
    }
}
