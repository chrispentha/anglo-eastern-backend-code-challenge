using ShipManagement.Application.Common;

namespace ShipManagement.Application.Users;

/// <summary>An application user.</summary>
public sealed record UserDto(int UserId, string FullName, string? Email, string Role, bool IsActive, DateTime CreatedAtUtc);

/// <summary>Request body for creating a user (US-01). Server-controlled fields (id, status, audit) are not bindable.</summary>
public sealed class CreateUserRequest
{
    /// <summary>Full name, 1-100 characters.</summary>
    public string? FullName { get; init; }

    /// <summary>Optional e-mail address, unique when supplied.</summary>
    public string? Email { get; init; }

    /// <summary>Role name: Administrator, FleetManager, Superintendent, CrewingOfficer, Accountant or OwnerRepresentative.</summary>
    public string? Role { get; init; }
}

/// <summary>Query string for listing users (US-02).</summary>
public sealed class ListUsersQuery
{
    public int? PageNumber { get; init; }

    public int? PageSize { get; init; }

    /// <summary>Optional role filter.</summary>
    public string? Role { get; init; }

    /// <summary>Sort by full name: asc (default) or desc.</summary>
    public string? SortDirection { get; init; }
}

/// <summary>User data access; every method maps to one stored procedure.</summary>
public interface IUserRepository
{
    Task<UserDto> CreateAsync(string fullName, string? email, string role, int requestedByUserId, CancellationToken cancellationToken);

    Task<UserDto> GetByIdAsync(int userId, int requestedByUserId, CancellationToken cancellationToken);

    Task<PagedResult<UserDto>> ListAsync(
        int pageNumber, int pageSize, string? role, string sortDirection, int requestedByUserId, CancellationToken cancellationToken);
}
