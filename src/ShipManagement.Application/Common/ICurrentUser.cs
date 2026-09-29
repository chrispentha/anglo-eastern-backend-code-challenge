namespace ShipManagement.Application.Common;

/// <summary>The authenticated caller of the current request.</summary>
public interface ICurrentUser
{
    int UserId { get; }

    bool IsAdministrator { get; }

    /// <summary>
    /// True when the caller is an administrator or the ship is assigned to them (SEC-04).
    /// The stored procedures enforce the same rule again (defence in depth).
    /// </summary>
    bool CanAccessShip(string shipCode);
}
