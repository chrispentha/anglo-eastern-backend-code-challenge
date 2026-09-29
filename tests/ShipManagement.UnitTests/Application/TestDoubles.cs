using ShipManagement.Application.Common;

namespace ShipManagement.UnitTests.Application;

/// <summary>A caller with a fixed identity and fixed ship assignments.</summary>
internal sealed class FakeCurrentUser(int userId, bool isAdministrator, params string[] shipCodes) : ICurrentUser
{
    public int UserId { get; } = userId;

    public bool IsAdministrator { get; } = isAdministrator;

    public bool CanAccessShip(string shipCode) =>
        IsAdministrator || shipCodes.Contains(shipCode, StringComparer.OrdinalIgnoreCase);

    public static FakeCurrentUser Admin() => new(1, isAdministrator: true);

    public static FakeCurrentUser AssignedTo(params string[] shipCodes) => new(42, false, shipCodes);
}

/// <summary>A clock frozen at a given instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
