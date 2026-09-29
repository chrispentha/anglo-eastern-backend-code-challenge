using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using ShipManagement.Infrastructure.Security;

namespace ShipManagement.UnitTests.Infrastructure;

public class ApiKeyTests
{
    private readonly ApiKeyGenerator _generator = new();

    [Fact]
    public void Generated_Key_Is_Well_Formed()
    {
        var key = _generator.Generate();

        ApiKeyFormat.IsWellFormed(key.RawKey).Should().BeTrue();
        key.RawKey.Should().StartWith($"sm_{key.Prefix}_").And.HaveLength(ApiKeyFormat.TotalLength);
        ApiKeyFormat.PrefixOf(key.RawKey).Should().Be(key.Prefix);
    }

    [Fact]
    public void Generated_Key_Hash_Is_Sha256_Of_The_Key()
    {
        var key = _generator.Generate();
        key.Hash.Should().Equal(SHA256.HashData(Encoding.ASCII.GetBytes(key.RawKey)));
        key.Hash.Should().HaveCount(32);
    }

    [Fact]
    public void Generated_Keys_Are_Unique()
    {
        var keys = Enumerable.Range(0, 1000).Select(_ => _generator.Generate().RawKey).ToList();
        keys.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sm_abcd1234_tooshort")]
    [InlineData("xx_abcd1234_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sm_ABCD1234_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sm_abcd1234-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sm_abcd1234_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'-")]
    [InlineData("sm_abcd1234_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA ")]
    public void Malformed_Keys_Are_Rejected_Before_Any_Lookup(string? key)
    {
        ApiKeyFormat.IsWellFormed(key).Should().BeFalse();
    }

    [Fact]
    public void Key_In_The_Format_Printed_By_The_Database_Is_Accepted()
    {
        // Same shape as 09_dev_api_keys.sql output: 32 random bytes, Base64Url, no padding.
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        ApiKeyFormat.IsWellFormed($"sm_devadm01_{secret}").Should().BeTrue();
    }
}
