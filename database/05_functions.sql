/*
    05_functions.sql
    Reusable business logic as INLINE table-valued functions (N2).
    Inline TVFs are expanded into the caller's query plan like a view, so they cost nothing at runtime,
    unlike scalar UDFs or multi-statement TVFs. Used with CROSS APPLY / JOIN.
    All are schema-bound: the tables they read cannot be altered underneath them.
*/

/* A schema-bound function cannot be altered while another schema-bound function references it,
   so the dependent function is dropped first and recreated at the end of this script (re-runnable deploy). */
DROP FUNCTION IF EXISTS dbo.tvf_FinancialLines;
GO

/*
    Crew status as of a date (D-01). Evaluation order matters:
      1. Sign Off set                      -> Signed Off
      2. Sign On after the as-of date      -> Planned
      3. More than 30 days past EOC        -> Relief Due
      4. Otherwise                         -> Onboard (includes 0-30 days past EOC, and Sign On = today)
*/
CREATE OR ALTER FUNCTION dbo.tvf_CrewStatus
(
    @SignOnDate         DATE,
    @SignOffDate        DATE,
    @EndOfContractDate  DATE,
    @AsOfDate           DATE
)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT CrewStatus = CAST(
        CASE
            WHEN @SignOffDate IS NOT NULL                              THEN 'Signed Off'
            WHEN @SignOnDate > @AsOfDate                               THEN 'Planned'
            WHEN @AsOfDate > DATEADD(DAY, 30, @EndOfContractDate)      THEN 'Relief Due'
            ELSE                                                            'Onboard'
        END AS VARCHAR(10));
GO

/*
    Age in completed years at the as-of date (D-19).
    Someone born on 29 Feb turns a year older on 28 Feb in non-leap years.
*/
CREATE OR ALTER FUNCTION dbo.tvf_AgeInYears
(
    @BirthDate  DATE,
    @AsOfDate   DATE
)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT Age = DATEDIFF(YEAR, @BirthDate, @AsOfDate)
               - CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, @BirthDate, @AsOfDate), @BirthDate) > @AsOfDate
                      THEN 1 ELSE 0 END;
GO

/*
    'dd MMM yyyy' label, e.g. '05 Apr 2025'. Built by hand because CONVERT style 106 depends on the
    session language and FORMAT() is slow (CLR). Used for display and for partial date search (D-14).
*/
CREATE OR ALTER FUNCTION dbo.tvf_DateLabel
(
    @Date DATE
)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT DateLabel = CAST(
               RIGHT('0' + CAST(DAY(@Date) AS VARCHAR(2)), 2) + ' '
             + SUBSTRING('JanFebMarAprMayJunJulAugSepOctNovDec', (MONTH(@Date) - 1) * 3 + 1, 3) + ' '
             + CAST(YEAR(@Date) AS CHAR(4)) AS VARCHAR(11));
GO

/*
    First month of the fiscal year that contains @Period.
    0403 + Feb 2025 -> 2024-04-01 (crosses the calendar year); 0403 + Apr 2025 -> 2025-04-01; 0112 + Jan 2025 -> 2025-01-01.
*/
CREATE OR ALTER FUNCTION dbo.tvf_FiscalYearStart
(
    @Period      DATE,
    @StartMonth  TINYINT
)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT FiscalYearStart = DATEFROMPARTS(
               YEAR(@Period) - CASE WHEN MONTH(@Period) >= @StartMonth THEN 0 ELSE 1 END,
               @StartMonth,
               1);
GO

/* Active user with role flags; empty when the user does not exist or is inactive. */
CREATE OR ALTER FUNCTION dbo.tvf_UserContext
(
    @UserId INT
)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT u.UserId, r.RoleName, r.IsAdministrator
    FROM dbo.AppUser AS u
    INNER JOIN dbo.AppRole AS r ON r.RoleId = u.RoleId
    WHERE u.UserId = @UserId
      AND u.IsActive = 1;
GO

/*
    The ship, if @UserId may access it: administrators see every ship, other users only ships assigned
    to them (SEC-04). "Does not exist" and "not yours" both return no row, so callers raise the same
    not-found error and ship codes cannot be enumerated (D-04).
*/
CREATE OR ALTER FUNCTION dbo.tvf_ShipForUser
(
    @UserId    INT,
    @ShipCode  VARCHAR(10)
)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT s.ShipId, s.ShipCode, s.ShipName, s.FiscalYearCode, fy.StartMonth, st.StatusName, st.IsOperational
    FROM dbo.Ship AS s
    INNER JOIN dbo.ShipStatus AS st ON st.ShipStatusId = s.ShipStatusId
    INNER JOIN dbo.FiscalYear AS fy ON fy.FiscalYearCode = s.FiscalYearCode
    INNER JOIN dbo.AppUser AS u ON u.UserId = @UserId AND u.IsActive = 1
    INNER JOIN dbo.AppRole AS r ON r.RoleId = u.RoleId
    WHERE s.ShipCode = @ShipCode
      AND (r.IsAdministrator = 1
           OR EXISTS (SELECT 1 FROM dbo.UserShip AS us WHERE us.UserId = u.UserId AND us.ShipId = s.ShipId));
GO

/*
    Account hierarchy: every account with its depth (roots = 1) and a materialised path of account
    numbers used to order a report so parents precede their children (tree order).
    The COA is small; MAXRECURSION default (100 levels) is far above any real chart depth.
*/
CREATE OR ALTER FUNCTION dbo.tvf_AccountTree()
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    WITH Tree AS
    (
        SELECT a.AccountId,
               HierarchyLevel = 1,
               SortPath = CAST(a.AccountNumber AS VARCHAR(900))
        FROM dbo.Account AS a
        WHERE a.ParentAccountId IS NULL

        UNION ALL

        SELECT c.AccountId,
               t.HierarchyLevel + 1,
               CAST(t.SortPath + '/' + c.AccountNumber AS VARCHAR(900))
        FROM dbo.Account AS c
        INNER JOIN Tree AS t ON c.ParentAccountId = t.AccountId
    )
    SELECT AccountId, HierarchyLevel, SortPath
    FROM Tree;
GO

/*
    Closure of the account tree: one row per (account, ancestor-or-self) pair.
    Grouping child-level amounts by AncestorId gives every parent the sum of ALL its descendants,
    at any depth (D-08).
*/
CREATE OR ALTER FUNCTION dbo.tvf_AccountClosure()
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    WITH Closure AS
    (
        SELECT DescendantId = a.AccountId, AncestorId = a.AccountId, a.ParentAccountId
        FROM dbo.Account AS a

        UNION ALL

        SELECT c.DescendantId, p.AccountId, p.ParentAccountId
        FROM Closure AS c
        INNER JOIN dbo.Account AS p ON p.AccountId = c.ParentAccountId
    )
    SELECT DescendantId, AncestorId
    FROM Closure;
GO

/*
    Financial report lines for one ship, one period and its fiscal year-to-date window.
    Shared by the Detail and Summary reports, so the two always tie out (D-09).

    NULL vs 0 (D-06): SUM over no rows is NULL (no data), SUM over rows is a number, possibly 0.
    Variance = Actual - Budget; NULL only when both sides are NULL.
    Row inclusion (D-07): any of the four amounts is non-NULL and non-zero. Amounts are never negative,
    so a parent qualifies exactly when at least one of its descendants does.
*/
CREATE OR ALTER FUNCTION dbo.tvf_FinancialLines
(
    @ShipId    INT,
    @Period    DATE,
    @YtdStart  DATE
)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    WITH Budget AS
    (
        SELECT b.AccountId,
               BudgetPeriod = SUM(CASE WHEN b.AccountPeriod = @Period THEN b.BudgetAmount END),
               BudgetYtd    = SUM(b.BudgetAmount)
        FROM dbo.BudgetEntry AS b
        WHERE b.ShipId = @ShipId
          AND b.AccountPeriod >= @YtdStart
          AND b.AccountPeriod <= @Period
        GROUP BY b.AccountId
    ),
    Actual AS
    (
        SELECT t.AccountId,
               ActualPeriod = SUM(CASE WHEN t.AccountPeriod = @Period THEN t.ActualAmount END),
               ActualYtd    = SUM(t.ActualAmount)
        FROM dbo.AccountTransaction AS t
        WHERE t.ShipId = @ShipId
          AND t.AccountPeriod >= @YtdStart
          AND t.AccountPeriod <= @Period
        GROUP BY t.AccountId
    ),
    Leaf AS
    (
        SELECT AccountId = COALESCE(b.AccountId, a.AccountId),
               a.ActualPeriod, b.BudgetPeriod, a.ActualYtd, b.BudgetYtd
        FROM Budget AS b
        FULL OUTER JOIN Actual AS a ON a.AccountId = b.AccountId
    ),
    Rolled AS
    (
        SELECT AccountId    = cl.AncestorId,
               ActualPeriod = SUM(l.ActualPeriod),
               BudgetPeriod = SUM(l.BudgetPeriod),
               ActualYtd    = SUM(l.ActualYtd),
               BudgetYtd    = SUM(l.BudgetYtd)
        FROM Leaf AS l
        INNER JOIN dbo.tvf_AccountClosure() AS cl ON cl.DescendantId = l.AccountId
        GROUP BY cl.AncestorId
    )
    SELECT acc.AccountId,
           acc.AccountNumber,
           acc.Description,
           acc.AccountType,
           tr.HierarchyLevel,
           ParentAccountNumber = par.AccountNumber,
           tr.SortPath,
           Actual      = r.ActualPeriod,
           Budget      = r.BudgetPeriod,
           Variance    = CASE WHEN r.ActualPeriod IS NULL AND r.BudgetPeriod IS NULL THEN NULL
                              ELSE ISNULL(r.ActualPeriod, 0) - ISNULL(r.BudgetPeriod, 0) END,
           ActualYtd   = r.ActualYtd,
           BudgetYtd   = r.BudgetYtd,
           VarianceYtd = CASE WHEN r.ActualYtd IS NULL AND r.BudgetYtd IS NULL THEN NULL
                              ELSE ISNULL(r.ActualYtd, 0) - ISNULL(r.BudgetYtd, 0) END
    FROM Rolled AS r
    INNER JOIN dbo.Account AS acc ON acc.AccountId = r.AccountId
    INNER JOIN dbo.tvf_AccountTree() AS tr ON tr.AccountId = r.AccountId
    LEFT JOIN dbo.Account AS par ON par.AccountId = acc.ParentAccountId
    WHERE r.ActualPeriod <> 0
       OR r.BudgetPeriod <> 0
       OR r.ActualYtd    <> 0
       OR r.BudgetYtd    <> 0;
GO
