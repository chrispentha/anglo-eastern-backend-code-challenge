using FluentValidation;
using ShipManagement.Application.Common;

namespace ShipManagement.Application.Ships;

internal static class ShipRules
{
    public const int ShipNameMaxLength = 100;
    public const int StatusMaxLength = 20;
    public const string FiscalYearCodePattern = "^[0-9]{4}$";

    public static readonly string ShipNameMessage = $"shipName is required and must be at most {ShipNameMaxLength} characters.";
    public static readonly string StatusMessage = $"status must be at most {StatusMaxLength} letters, e.g. Active or Inactive.";

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= ShipNameMaxLength;

    public static bool IsValidStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || (status.Trim().Length <= StatusMaxLength && status.Trim().All(char.IsAsciiLetter));
}

public sealed class CreateShipRequestValidator : AbstractValidator<CreateShipRequest>
{
    public CreateShipRequestValidator()
    {
        RuleFor(r => r.ShipCode).ValidShipCode();
        RuleFor(r => r.ShipName).Must(ShipRules.IsValidName).WithMessage(ShipRules.ShipNameMessage);
        RuleFor(r => r.FiscalYearCode)
            .NotEmpty().WithMessage("fiscalYearCode is required.")
            .Matches(ShipRules.FiscalYearCodePattern)
            .WithMessage("fiscalYearCode must be 4 digits MMNN, e.g. 0112 (Jan-Dec) or 0403 (Apr-Mar).");
        RuleFor(r => r.Status).Must(ShipRules.IsValidStatus).WithMessage(ShipRules.StatusMessage);
    }
}

public sealed class UpdateShipRequestValidator : AbstractValidator<UpdateShipRequest>
{
    public UpdateShipRequestValidator()
    {
        RuleFor(r => r)
            .Must(r => r.ShipName is not null || !string.IsNullOrWhiteSpace(r.Status))
            .WithName("request")
            .WithMessage("At least one of shipName or status must be provided.");
        RuleFor(r => r.ShipName)
            .Must(ShipRules.IsValidName).WithMessage(ShipRules.ShipNameMessage)
            .When(r => r.ShipName is not null);
        RuleFor(r => r.Status).Must(ShipRules.IsValidStatus).WithMessage(ShipRules.StatusMessage);
    }
}

public sealed class ListShipsQueryValidator : AbstractValidator<ListShipsQuery>
{
    public ListShipsQueryValidator()
    {
        RuleFor(q => q.PageNumber).ValidPageNumber();
        RuleFor(q => q.PageSize).ValidPageSize();
        RuleFor(q => q.Status).Must(ShipRules.IsValidStatus).WithMessage(ShipRules.StatusMessage);
    }
}
