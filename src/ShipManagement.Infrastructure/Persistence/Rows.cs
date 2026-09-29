namespace ShipManagement.Infrastructure.Persistence;

// Row types mirror the stored procedures' result sets column-for-column; repositories map them to DTOs.
// Kept separate from the DTOs so a database column rename never silently changes the public API contract.

internal sealed class UserRow
{
    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

internal sealed class ShipRow
{
    public string ShipCode { get; set; } = string.Empty;

    public string ShipName { get; set; } = string.Empty;

    public string FiscalYearCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

internal sealed class AssignedShipRow
{
    public string ShipCode { get; set; } = string.Empty;

    public string ShipName { get; set; } = string.Empty;

    public string FiscalYearCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime AssignedAtUtc { get; set; }
}

internal sealed class CrewRow
{
    public string RankName { get; set; } = string.Empty;

    public string CrewMemberId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public int Age { get; set; }

    public string Nationality { get; set; } = string.Empty;

    public DateTime SignOnDate { get; set; }

    public string SignOnDateLabel { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

internal sealed class FinancialLineRow
{
    public string? AccountNumber { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? AccountType { get; set; }

    public int HierarchyLevel { get; set; }

    public string? ParentAccountNumber { get; set; }

    public decimal? Actual { get; set; }

    public decimal? Budget { get; set; }

    public decimal? Variance { get; set; }

    public decimal? ActualYtd { get; set; }

    public decimal? BudgetYtd { get; set; }

    public decimal? VarianceYtd { get; set; }

    public bool IsTotal { get; set; }
}

internal sealed class ApiKeyRow
{
    public int ApiKeyId { get; set; }

    public int UserId { get; set; }

    public string KeyPrefix { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }
}

internal sealed class AuthUserRow
{
    public int UserId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool IsAdministrator { get; set; }
}
