using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Application.Common;

/// <summary>Validation messages shared by several validators, so the same rule reads the same everywhere.</summary>
public static class ValidationMessages
{
    public const string ShipCode = "shipCode must be 3 to 10 letters or digits.";
    public const string Period = "period is required and must be in yyyy-MM format between 2000-01 and 2099-12, e.g. 2025-07.";
    public const string SortDirection = "sortDirection must be 'asc' or 'desc'.";
    public static readonly string PageNumber = $"pageNumber must be between 1 and {PageRequest.MaxPageNumber}.";
    public static readonly string PageSize = $"pageSize must be between 1 and {PageRequest.MaxPageSize}.";
}
