using FluentValidation;
using Microsoft.Extensions.Logging;
using ShipManagement.Application.Common;
using ShipManagement.Domain.Errors;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Application.Crew;

/// <summary>A crew member currently Onboard or Relief Due on a ship. Birth date is not exposed, only age (SEC-08).</summary>
public sealed record CrewMemberDto(
    string RankName,
    string CrewMemberId,
    string FirstName,
    string LastName,
    int Age,
    string Nationality,
    DateOnly SignOnDate,
    string SignOnDateLabel,
    string Status);

/// <summary>Query string for the crew list (US-07).</summary>
public sealed class CrewListQuery
{
    public int? PageNumber { get; init; }

    public int? PageSize { get; init; }

    /// <summary>rank (seniority, default), crewMemberId, firstName, lastName, age, nationality, signOnDate or status.</summary>
    public string? SortBy { get; init; }

    /// <summary>asc (default) or desc.</summary>
    public string? SortDirection { get; init; }

    /// <summary>Case-insensitive "contains" search over every column except status, e.g. "05 Apr" or "phil".</summary>
    public string? Search { get; init; }
}

/// <summary>Validated, normalised crew list criteria passed to the repository.</summary>
public sealed record CrewListCriteria(int PageNumber, int PageSize, string SortBy, string SortDirection, string? Search);

public interface ICrewRepository
{
    Task<PagedResult<CrewMemberDto>> ListByShipAsync(
        string shipCode, CrewListCriteria criteria, int requestedByUserId, CancellationToken cancellationToken);
}

public sealed class CrewListQueryValidator : AbstractValidator<CrewListQuery>
{
    public const int SearchMaxLength = 100;

    public CrewListQueryValidator()
    {
        RuleFor(q => q.PageNumber).ValidPageNumber();
        RuleFor(q => q.PageSize).ValidPageSize();
        RuleFor(q => q.SortBy)
            .Must(s => CrewListSort.NormalizeSortBy(s) is not null)
            .WithMessage($"sortBy must be one of: {string.Join(", ", CrewListSort.SortKeys)}.");
        RuleFor(q => q.SortDirection)
            .Must(d => Domain.ValueObjects.SortDirection.Normalize(d) is not null)
            .WithMessage(ValidationMessages.SortDirection);
        RuleFor(q => q.Search)
            .Must(s => s is null || s.Trim().Length <= SearchMaxLength)
            .WithMessage($"search must be at most {SearchMaxLength} characters.");
    }
}

/// <summary>Crew list for a ship (US-07).</summary>
public sealed partial class CrewService(
    ICrewRepository repository,
    ICurrentUser currentUser,
    IValidator<CrewListQuery> validator,
    ILogger<CrewService> logger)
{
    public async Task<PagedResult<CrewMemberDto>> ListAsync(string shipCode, CrewListQuery query, CancellationToken cancellationToken)
    {
        var code = ValidationExtensions.ParseShipCode(shipCode);
        await validator.EnsureValidAsync(query, cancellationToken);

        if (!currentUser.CanAccessShip(code))
        {
            throw new NotFoundException(ErrorCodes.ShipNotFound, $"Ship '{code}' was not found.");
        }

        var criteria = new CrewListCriteria(
            query.PageNumber ?? PageRequest.DefaultPageNumber,
            query.PageSize ?? PageRequest.DefaultPageSize,
            CrewListSort.NormalizeSortBy(query.SortBy)!,
            Domain.ValueObjects.SortDirection.Normalize(query.SortDirection)!,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim());

        var result = await repository.ListByShipAsync(code, criteria, currentUser.UserId, cancellationToken);

        // The search term is deliberately not logged: it may contain a person's name (SEC-09).
        LogCrewListRequested(logger, code, currentUser.UserId, result.TotalCount);
        return result;
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
        Message = "Crew list for ship {ShipCode} requested by user {ActorUserId} ({TotalCount} crew)")]
    private static partial void LogCrewListRequested(ILogger logger, string shipCode, int actorUserId, int totalCount);
}
