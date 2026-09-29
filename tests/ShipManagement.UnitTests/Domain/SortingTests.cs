using FluentAssertions;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.UnitTests.Domain;

public class SortingTests
{
    [Theory]
    [InlineData(null, "rank")]
    [InlineData("", "rank")]
    [InlineData("RANK", "rank")]
    [InlineData("lastname", "lastName")]
    [InlineData(" SignOnDate ", "signOnDate")]
    [InlineData("crewmemberid", "crewMemberId")]
    public void SortBy_Is_Case_Insensitive_And_Canonicalised(string? input, string expected)
    {
        CrewListSort.NormalizeSortBy(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("birthDate")]
    [InlineData("LastName; DROP TABLE dbo.Ship;--")]
    [InlineData("1")]
    [InlineData("rank desc")]
    public void SortBy_Rejects_Anything_Outside_The_Whitelist(string input)
    {
        CrewListSort.NormalizeSortBy(input).Should().BeNull();
    }

    [Theory]
    [InlineData(null, "asc")]
    [InlineData("ASC", "asc")]
    [InlineData("Desc", "desc")]
    public void SortDirection_Is_Case_Insensitive(string? input, string expected)
    {
        SortDirection.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("ascending")]
    [InlineData("asc;--")]
    [InlineData("up")]
    public void SortDirection_Rejects_Unknown_Values(string input)
    {
        SortDirection.Normalize(input).Should().BeNull();
    }
}
