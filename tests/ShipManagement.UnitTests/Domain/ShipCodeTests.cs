using FluentAssertions;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.UnitTests.Domain;

public class ShipCodeTests
{
    [Theory]
    [InlineData("SHIP01", "SHIP01")]
    [InlineData("ship01", "SHIP01")]
    [InlineData("  Ship01 ", "SHIP01")]
    [InlineData("ABC", "ABC")]
    [InlineData("ABCDEFGHIJ", "ABCDEFGHIJ")]
    public void ShipCode_Is_Normalised_To_Trimmed_Upper_Case(string input, string expected)
    {
        ShipCode.TryParse(input, out var code).Should().BeTrue();
        code!.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AB")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("SHIP-01")]
    [InlineData("SHIP 01")]
    [InlineData("SHIP01'--")]
    [InlineData("ŞHIP01")]
    public void ShipCode_Is_Rejected_When_Not_3_To_10_Letters_Or_Digits(string? input)
    {
        ShipCode.TryParse(input, out var code).Should().BeFalse();
        code.Should().BeNull();
    }
}
