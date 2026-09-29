using FluentValidation;
using Microsoft.Extensions.Logging;
using ShipManagement.Application.Common;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Application.Users;

/// <summary>User use cases: create (US-01), list (US-02), read.</summary>
public sealed partial class UserService(
    IUserRepository repository,
    ICurrentUser currentUser,
    IValidator<CreateUserRequest> createValidator,
    IValidator<ListUsersQuery> listValidator,
    ILogger<UserService> logger)
{
    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);

        var user = await repository.CreateAsync(
            request.FullName!.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            request.Role!.Trim(),
            currentUser.UserId,
            cancellationToken);

        LogUserCreated(logger, user.UserId, user.Role, currentUser.UserId);
        return user;
    }

    public Task<UserDto> GetByIdAsync(int userId, CancellationToken cancellationToken)
    {
        ValidationExtensions.EnsurePositiveId(userId, "userId");
        return repository.GetByIdAsync(userId, currentUser.UserId, cancellationToken);
    }

    public Task<UserDto> GetCurrentAsync(CancellationToken cancellationToken) =>
        repository.GetByIdAsync(currentUser.UserId, currentUser.UserId, cancellationToken);

    public async Task<PagedResult<UserDto>> ListAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken);

        return await repository.ListAsync(
            query.PageNumber ?? PageRequest.DefaultPageNumber,
            query.PageSize ?? PageRequest.DefaultPageSize,
            string.IsNullOrWhiteSpace(query.Role) ? null : query.Role.Trim(),
            SortDirection.Normalize(query.SortDirection)!,
            currentUser.UserId,
            cancellationToken);
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "User {UserId} created with role {Role} by user {ActorUserId}")]
    private static partial void LogUserCreated(ILogger logger, int userId, string role, int actorUserId);
}
