using FluentValidation;
using ShipManagement.Application.Common;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Application.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public const int FullNameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int RoleMaxLength = 30;

    public CreateUserRequestValidator()
    {
        RuleFor(r => r.FullName)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= FullNameMaxLength)
            .WithMessage($"fullName is required and must be at most {FullNameMaxLength} characters.");

        RuleFor(r => r.Email)
            .MaximumLength(EmailMaxLength).WithMessage($"email must be at most {EmailMaxLength} characters.")
            .EmailAddress().WithMessage("email must be a valid e-mail address.")
            .When(r => !string.IsNullOrWhiteSpace(r.Email));

        RuleFor(r => r.Role)
            .NotEmpty().WithMessage("role is required.")
            .MaximumLength(RoleMaxLength).WithMessage($"role must be at most {RoleMaxLength} characters.");
    }
}

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(q => q.PageNumber).ValidPageNumber();
        RuleFor(q => q.PageSize).ValidPageSize();
        RuleFor(q => q.Role).MaximumLength(CreateUserRequestValidator.RoleMaxLength)
            .WithMessage($"role must be at most {CreateUserRequestValidator.RoleMaxLength} characters.");
        RuleFor(q => q.SortDirection)
            .Must(d => SortDirection.Normalize(d) is not null)
            .WithMessage(ValidationMessages.SortDirection);
    }
}
