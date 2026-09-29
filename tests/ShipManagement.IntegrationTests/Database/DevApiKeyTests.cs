using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using ShipManagement.IntegrationTests.Infrastructure;

namespace ShipManagement.IntegrationTests.Database;

/// <summary>
/// 09_dev_api_keys.sql: local development keys are random, printed once, stored only as hashes,
/// kept across redeployments and replaced on rotation (D-24).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed partial class DevApiKeyTests(DatabaseFixture db)
{
    private const string ActiveDevKeys = """
        SELECT COUNT(*) FROM dbo.ApiKey
        WHERE KeyPrefix LIKE 'dev%' AND RevokedAtUtc IS NULL AND ExpiresAtUtc > SYSUTCDATETIME()
        """;

    private static List<string> PrintedKeys(string deployOutput) =>
        DevKey().Matches(deployOutput).Select(m => m.Value).ToList();

    [Fact]
    public void First_Deployment_Prints_One_Random_Key_Per_Sample_User()
    {
        var keys = PrintedKeys(db.InitialDeployOutput);

        keys.Should().HaveCount(6).And.OnlyHaveUniqueItems();
        keys.Select(k => k[..12]).Should().BeEquivalentTo(
            ["sm_devadm01_", "sm_devcrw01_", "sm_devacc01_", "sm_devown01_", "sm_devsup01_", "sm_devflt01_"]);
    }

    [Fact]
    public async Task Printed_Keys_Are_Stored_Only_As_Hashes()
    {
        var key = PrintedKeys(db.InitialDeployOutput).First(k => k.StartsWith("sm_devadm01_", StringComparison.Ordinal));

        var rawStored = await db.QueryAdminScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.ApiKey WHERE CAST(KeyHash AS VARCHAR(100)) = @key OR KeyPrefix = @key", new { key });
        var hashStored = await db.QueryAdminScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.ApiKey WHERE KeyHash = HASHBYTES('SHA2_256', CAST(@key AS VARCHAR(100)))", new { key });

        rawStored.Should().Be(0);
        hashStored.Should().Be(1);
    }

    [Fact]
    public async Task Redeployment_Keeps_Keys_And_Rotation_Replaces_Them()
    {
        var activeBefore = await db.QueryAdminScalarAsync<int>(ActiveDevKeys);

        var redeploy = await db.RunDeployAsync();
        PrintedKeys(redeploy).Should().BeEmpty("existing keys are kept and never printed again");
        (await db.QueryAdminScalarAsync<int>(ActiveDevKeys)).Should().Be(activeBefore);

        var rotated = PrintedKeys(await db.RunDeployAsync(rotateDevKeys: true));
        rotated.Should().HaveCount(6);
        (await db.QueryAdminScalarAsync<int>(ActiveDevKeys)).Should().Be(6);

        // A rotated key works through the real API (hashing in C# matches hashing in SQL).
        var admin = rotated.First(k => k.StartsWith("sm_devadm01_", StringComparison.Ordinal));
        (await db.Api.CreateClient(admin).GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [GeneratedRegex("sm_dev[a-z0-9]{5}_[A-Za-z0-9_-]{43}")]
    private static partial Regex DevKey();
}
