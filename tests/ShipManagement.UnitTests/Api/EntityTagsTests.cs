using FluentAssertions;
using ShipManagement.Api.Http;
using ShipManagement.Domain.Errors;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.UnitTests.Api;

public class EntityTagsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData(" * ")]
    public void No_Precondition_When_If_Match_Is_Absent_Or_Wildcard(string? header)
    {
        EntityTags.ParseIfMatch(header).Should().BeNull();
    }

    [Fact]
    public void Strong_Etag_Is_Parsed_To_The_Row_Version()
    {
        EntityTags.ParseIfMatch("\"00000000000007D3\"").Should().Be(new RowVersion(2003));
        EntityTags.Format("00000000000007D3").Should().Be("\"00000000000007D3\"");
    }

    [Theory]
    [InlineData("00000000000007D3")]
    [InlineData("\"00000000000007D3")]
    [InlineData("\"00000000000007D3\", \"0000000000000001\"")]
    public void Malformed_If_Match_Is_A_Validation_Error(string header)
    {
        var act = () => EntityTags.ParseIfMatch(header);
        act.Should().Throw<RequestValidationException>().Which.Errors.Should().ContainKey("If-Match");
    }

    [Theory]
    [InlineData("W/\"00000000000007D3\"")]  // weak tags never match under strong comparison
    [InlineData("\"not-a-version\"")]
    public void Etag_That_Can_Never_Match_Fails_The_Precondition(string header)
    {
        var act = () => EntityTags.ParseIfMatch(header);
        act.Should().Throw<PreconditionFailedException>();
    }
}

public class RowVersionTests
{
    [Fact]
    public void Round_Trips_Through_Bytes_And_Hex()
    {
        var bytes = new byte[] { 0, 0, 0, 0, 0, 0, 0x07, 0xD3 };

        var version = RowVersion.FromBytes(bytes);

        version.Value.Should().Be(2003);
        version.ToString().Should().Be("00000000000007D3");
        version.ToBytes().Should().Equal(bytes);
        RowVersion.TryParse("00000000000007D3", out var parsed).Should().BeTrue();
        parsed.Should().Be(version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("7D3")]
    [InlineData("00000000000007D3FF")]
    [InlineData("00000000000007G3")]
    public void Rejects_Anything_But_16_Hex_Characters(string? hex)
    {
        RowVersion.TryParse(hex, out _).Should().BeFalse();
    }

    [Fact]
    public void Rejects_Byte_Arrays_Of_The_Wrong_Length()
    {
        var act = () => RowVersion.FromBytes(new byte[4]);
        act.Should().Throw<ArgumentException>();
    }
}
