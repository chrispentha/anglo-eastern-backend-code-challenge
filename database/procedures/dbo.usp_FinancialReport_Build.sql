/*
    Shared implementation of the Detail and Summary financial reports (internal: lives in [dbo], so the
    API login cannot call it directly; the [app] wrappers reach it through ownership chaining).

    For one ship and one accounting period:
      Actual, Budget, Variance (Actual - Budget) for the period, and the same for fiscal year-to-date.
      YTD runs from the first month of the ship's fiscal year that contains @Period up to @Period,
      e.g. fiscal year 0403 and Feb 2025 -> Apr 2024 .. Feb 2025.
      Parent accounts = sum of all descendant child accounts (D-08); NULL = no data, 0 = zero (D-06);
      only rows with a non-zero amount are returned (D-07).

    @ParentsOnly = 0 -> Detail : every qualifying account, parents and children, in tree order.
    @ParentsOnly = 1 -> Summary: qualifying parent (summary) accounts only, in tree order (D-09).
    Both end with a Grand Total row (IsTotal = 1): the sum of the root accounts.
*/
CREATE OR ALTER PROCEDURE dbo.usp_FinancialReport_Build
    @ShipCode           VARCHAR(20),
    @Period             DATE,
    @RequestedByUserId  INT,
    @ParentsOnly        BIT,
    @YtdStart           DATE = NULL OUTPUT,
    @YtdEnd             DATE = NULL OUTPUT,
    @FiscalYearCode     CHAR(4) = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @ShipCode = UPPER(LTRIM(RTRIM(@ShipCode)));

    IF @ShipCode IS NULL OR LEN(@ShipCode) NOT BETWEEN 3 AND 10
       OR @ShipCode COLLATE Latin1_General_BIN2 LIKE '%[^A-Z0-9]%'
        THROW 50001, N'VALIDATION_FAILED|shipCode must be 3 to 10 letters or digits.', 1;
    IF @Period IS NULL OR DAY(@Period) <> 1 OR @Period < '2000-01-01' OR @Period > '2099-12-01'
        THROW 50001, N'VALIDATION_FAILED|period must be the first day of a month between 2000-01 and 2099-12.', 1;

    DECLARE @ShipId INT, @IsOperational BIT, @StartMonth TINYINT;

    SELECT @ShipId = ShipId, @IsOperational = IsOperational, @StartMonth = StartMonth, @FiscalYearCode = FiscalYearCode
    FROM dbo.tvf_ShipForUser(@RequestedByUserId, @ShipCode);

    IF @ShipId IS NULL
    BEGIN
        DECLARE @NotFound NVARCHAR(2048) = CONCAT(N'SHIP_NOT_FOUND|Ship ''', @ShipCode, N''' was not found.');
        THROW 50002, @NotFound, 1;
    END;
    IF @IsOperational = 0
    BEGIN
        DECLARE @Inactive NVARCHAR(2048) = CONCAT(N'SHIP_INACTIVE|Ship ''', @ShipCode, N''' is inactive; financial reports are only available for active ships.');
        THROW 50004, @Inactive, 1;
    END;

    SELECT @YtdStart = FiscalYearStart FROM dbo.tvf_FiscalYearStart(@Period, @StartMonth);
    SET @YtdEnd = @Period;

    SELECT AccountNumber, Description, AccountType, HierarchyLevel, ParentAccountNumber, SortPath,
           Actual, Budget, Variance, ActualYtd, BudgetYtd, VarianceYtd
    INTO #Lines
    FROM dbo.tvf_FinancialLines(@ShipId, @Period, @YtdStart);

    SELECT AccountNumber, Description, AccountType, HierarchyLevel, ParentAccountNumber,
           Actual, Budget, Variance, ActualYtd, BudgetYtd, VarianceYtd, IsTotal
    FROM
    (
        SELECT AccountNumber, Description, AccountType, HierarchyLevel, ParentAccountNumber,
               Actual, Budget, Variance, ActualYtd, BudgetYtd, VarianceYtd,
               IsTotal = CAST(0 AS BIT), SortPath
        FROM #Lines
        WHERE @ParentsOnly = 0 OR AccountType = 'P'

        UNION ALL

        -- Grand total over root accounts, with the same NULL semantics as every other row.
        SELECT NULL, N'GRAND TOTAL', NULL, 0, NULL,
               SUM(Actual), SUM(Budget),
               CASE WHEN SUM(Actual) IS NULL AND SUM(Budget) IS NULL THEN NULL
                    ELSE ISNULL(SUM(Actual), 0) - ISNULL(SUM(Budget), 0) END,
               SUM(ActualYtd), SUM(BudgetYtd),
               CASE WHEN SUM(ActualYtd) IS NULL AND SUM(BudgetYtd) IS NULL THEN NULL
                    ELSE ISNULL(SUM(ActualYtd), 0) - ISNULL(SUM(BudgetYtd), 0) END,
               CAST(1 AS BIT), NULL
        FROM #Lines
        WHERE HierarchyLevel = 1
        HAVING COUNT(*) > 0
    ) AS Report
    ORDER BY IsTotal, SortPath;
END;
GO
