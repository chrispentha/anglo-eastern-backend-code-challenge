namespace ShipManagement.IntegrationTests.Infrastructure;

/// <summary>Known values from Fixtures/test_data.sql.</summary>
internal static class TestData
{
    /// <summary>The pinned "today" the fixture crew dates were designed around.</summary>
    public static readonly DateTime AsOfDate = new(2025, 6, 15);

    public const string AdminEmail = "test.admin@example.test";
    public const string CrewingEmail = "test.crewing@example.test";
    public const string OtherEmail = "test.other@example.test";
    public const string RateLimitEmail = "test.ratelimit@example.test";

    public static readonly string AdminKey = Key("tstadm01", 'A');
    public static readonly string CrewingKey = Key("tstcrw01", 'B');
    public static readonly string OtherKey = Key("tstoth01", 'C');
    public static readonly string InactiveUserKey = Key("tstina01", 'D');
    public static readonly string RevokedKey = Key("tstrev01", 'E');
    public static readonly string ExpiredKey = Key("tstexp01", 'F');
    public static readonly string RateLimitKey = Key("tstrat01", 'G');

    /// <summary>Crew members listed on TST01 as of <see cref="AsOfDate"/>, in rank-seniority order.</summary>
    public static readonly string[] Tst01CrewBySeniority =
        ["TC001", "TC002", "TC003", "TC011", "TC007", "TC008", "TC009", "TC013", "TC012", "TC010"];

    private static string Key(string prefix, char fill) => $"sm_{prefix}_{new string(fill, 43)}";
}
