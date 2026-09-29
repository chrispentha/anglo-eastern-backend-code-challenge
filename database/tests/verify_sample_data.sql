/*
    verify_sample_data.sql
    Asserts that the sample data meets every minimum in the brief and the integrity rules that cannot be
    expressed as constraints. Fails loudly (THROW 50100) so deploy.sh and CI stop on bad data.

    Per-ship minimums apply to the sample fleet only: ships created later through the API start with no crew
    or finance data, and must not make a re-deployment fail. Integrity rules apply to all data.
*/
SET NOCOUNT ON;

DECLARE @Today DATE = CAST(SYSUTCDATETIME() AS DATE);
DECLARE @Failures TABLE (FailedRule NVARCHAR(400));

DECLARE @SampleShip TABLE (ShipId INT PRIMARY KEY, ShipCode VARCHAR(10), FiscalYearCode CHAR(4), IsOperational BIT);
INSERT @SampleShip
SELECT s.ShipId, s.ShipCode, s.FiscalYearCode, st.IsOperational
FROM dbo.Ship AS s
JOIN dbo.ShipStatus AS st ON st.ShipStatusId = s.ShipStatusId
WHERE s.ShipCode IN ('SHIP01', 'SHIP02', 'SHIP03', 'SHIP04', 'SHIP05');

/* ---- Ships: >= 5, active and inactive, fiscal years 0112 and 0403 ---- */
IF (SELECT COUNT(*) FROM @SampleShip) < 5
    INSERT @Failures VALUES (N'At least 5 ships are required.');
IF NOT EXISTS (SELECT 1 FROM @SampleShip WHERE IsOperational = 1)
   OR NOT EXISTS (SELECT 1 FROM @SampleShip WHERE IsOperational = 0)
    INSERT @Failures VALUES (N'Ships must include both active and inactive statuses.');
IF (SELECT COUNT(DISTINCT FiscalYearCode) FROM @SampleShip WHERE FiscalYearCode IN ('0112', '0403')) < 2
    INSERT @Failures VALUES (N'Ships must cover fiscal years 0112 and 0403.');

/* ---- Crew: >= 20 per ship currently on board (started, not signed off) ---- */
INSERT @Failures
SELECT CONCAT(N'Ship ', s.ShipCode, N' has fewer than 20 crew on board.')
FROM @SampleShip AS s
WHERE (SELECT COUNT(DISTINCT h.CrewMemberId) FROM dbo.CrewServiceHistory AS h
       WHERE h.ShipId = s.ShipId AND h.SignOffDate IS NULL AND h.SignOnDate <= @Today) < 20;

/* ---- Ranks: >= 5 rank types, and every ship uses >= 5 of them ---- */
IF (SELECT COUNT(*) FROM dbo.CrewRank) < 5
    INSERT @Failures VALUES (N'At least 5 rank types are required.');
INSERT @Failures
SELECT CONCAT(N'Ship ', s.ShipCode, N' uses fewer than 5 distinct ranks.')
FROM @SampleShip AS s
WHERE (SELECT COUNT(DISTINCT h.RankId) FROM dbo.CrewServiceHistory AS h WHERE h.ShipId = s.ShipId) < 5;

/* ---- Realistic service history: every status occurs ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.CrewServiceHistory AS h
               CROSS APPLY dbo.tvf_CrewStatus(h.SignOnDate, h.SignOffDate, h.EndOfContractDate, @Today) AS st
               WHERE st.CrewStatus = 'Onboard')
    INSERT @Failures VALUES (N'No Onboard crew.');
IF NOT EXISTS (SELECT 1 FROM dbo.CrewServiceHistory AS h
               CROSS APPLY dbo.tvf_CrewStatus(h.SignOnDate, h.SignOffDate, h.EndOfContractDate, @Today) AS st
               WHERE st.CrewStatus = 'Relief Due')
    INSERT @Failures VALUES (N'No Relief Due crew.');
IF NOT EXISTS (SELECT 1 FROM dbo.CrewServiceHistory AS h
               CROSS APPLY dbo.tvf_CrewStatus(h.SignOnDate, h.SignOffDate, h.EndOfContractDate, @Today) AS st
               WHERE st.CrewStatus = 'Planned')
    INSERT @Failures VALUES (N'No Planned crew.');
IF NOT EXISTS (SELECT 1 FROM dbo.CrewServiceHistory WHERE SignOffDate IS NOT NULL)
    INSERT @Failures VALUES (N'No Signed Off crew.');

/* ---- Integrity: a seafarer can never serve two overlapping contracts ---- */
IF EXISTS (
    SELECT 1
    FROM dbo.CrewServiceHistory AS a
    JOIN dbo.CrewServiceHistory AS b
      ON b.CrewMemberId = a.CrewMemberId
     AND b.CrewServiceId > a.CrewServiceId
     AND a.SignOnDate <= ISNULL(b.SignOffDate, '9999-12-31')
     AND b.SignOnDate <= ISNULL(a.SignOffDate, '9999-12-31'))
    INSERT @Failures VALUES (N'A crew member has overlapping contracts.');

/* ---- Chart of Accounts: >= 5 parents with >= 5 children each; >= 2 levels ---- */
IF (SELECT COUNT(*) FROM dbo.Account AS p
    WHERE p.AccountType = 'P'
      AND (SELECT COUNT(*) FROM dbo.Account AS c WHERE c.ParentAccountId = p.AccountId) >= 5) < 5
    INSERT @Failures VALUES (N'At least 5 parent accounts with at least 5 children each are required.');
IF (SELECT MAX(HierarchyLevel) FROM dbo.tvf_AccountTree()) < 2
    INSERT @Failures VALUES (N'The Chart of Accounts must have at least 2 levels.');
IF (SELECT COUNT(*) FROM dbo.tvf_AccountTree()) <> (SELECT COUNT(*) FROM dbo.Account)
    INSERT @Failures VALUES (N'The Chart of Accounts contains a cycle or an unreachable account.');

/* ---- Budgets: >= 3 ships incl. 0112 and 0403, all 24 months of 2024-2025, >= 5 accounts ---- */
DECLARE @BudgetShips TABLE (ShipId INT PRIMARY KEY, FiscalYearCode CHAR(4));
INSERT @BudgetShips
SELECT s.ShipId, s.FiscalYearCode
FROM @SampleShip AS s
WHERE (SELECT COUNT(*) FROM
          (SELECT b.AccountId FROM dbo.BudgetEntry AS b
           WHERE b.ShipId = s.ShipId AND b.AccountPeriod BETWEEN '2024-01-01' AND '2025-12-01'
           GROUP BY b.AccountId
           HAVING COUNT(DISTINCT b.AccountPeriod) = 24) AS full_accounts) >= 5;
IF (SELECT COUNT(*) FROM @BudgetShips) < 3
    INSERT @Failures VALUES (N'Budgets must cover all of 2024 and 2025 for at least 5 accounts on at least 3 ships.');
IF (SELECT COUNT(DISTINCT FiscalYearCode) FROM @BudgetShips WHERE FiscalYearCode IN ('0112', '0403')) < 2
    INSERT @Failures VALUES (N'Budget ships must include fiscal years 0112 and 0403.');

/* ---- Actuals: >= 3 ships incl. 0112 and 0403, all of 2024 and >= 6 months of 2025 ---- */
DECLARE @ActualShips TABLE (ShipId INT PRIMARY KEY, FiscalYearCode CHAR(4));
INSERT @ActualShips
SELECT s.ShipId, s.FiscalYearCode
FROM @SampleShip AS s
WHERE (SELECT COUNT(DISTINCT t.AccountPeriod) FROM dbo.AccountTransaction AS t
       WHERE t.ShipId = s.ShipId AND YEAR(t.AccountPeriod) = 2024) = 12
  AND (SELECT COUNT(DISTINCT t.AccountPeriod) FROM dbo.AccountTransaction AS t
       WHERE t.ShipId = s.ShipId AND YEAR(t.AccountPeriod) = 2025) >= 6;
IF (SELECT COUNT(*) FROM @ActualShips) < 3
    INSERT @Failures VALUES (N'Actuals must cover all of 2024 and at least 6 months of 2025 on at least 3 ships.');
IF (SELECT COUNT(DISTINCT FiscalYearCode) FROM @ActualShips WHERE FiscalYearCode IN ('0112', '0403')) < 2
    INSERT @Failures VALUES (N'Actual ships must include fiscal years 0112 and 0403.');

/* ---- Finance ships are the crew ships ---- */
IF EXISTS (SELECT 1 FROM @BudgetShips AS b
           WHERE NOT EXISTS (SELECT 1 FROM dbo.CrewServiceHistory AS h WHERE h.ShipId = b.ShipId)
              OR NOT EXISTS (SELECT 1 FROM @ActualShips AS a WHERE a.ShipId = b.ShipId))
    INSERT @Failures VALUES (N'Budget, actual and crew data must cover the same ships.');

/* ---- The brief's own records are present ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.Ship WHERE ShipCode = 'SHIP01' AND ShipName = N'Flying Dutchman' AND FiscalYearCode = '0112')
   OR NOT EXISTS (SELECT 1 FROM dbo.Ship WHERE ShipCode = 'SHIP02' AND ShipName = N'Thousand Sunny' AND FiscalYearCode = '0403')
   OR NOT EXISTS (SELECT 1 FROM dbo.CrewServiceHistory WHERE CrewMemberId = 'CREW001' AND SignOnDate = '2025-04-05' AND EndOfContractDate = '2025-07-05')
   OR NOT EXISTS (SELECT 1 FROM dbo.CrewServiceHistory WHERE CrewMemberId = 'CREW002' AND SignOnDate = '2025-04-04' AND EndOfContractDate = '2025-07-04')
   OR (SELECT SUM(t.ActualAmount) FROM dbo.AccountTransaction AS t
       JOIN dbo.Ship AS s ON s.ShipId = t.ShipId JOIN dbo.Account AS a ON a.AccountId = t.AccountId
       WHERE s.ShipCode = 'SHIP01' AND a.AccountNumber = '7135000' AND t.AccountPeriod = '2025-01-01') <> 1000
    INSERT @Failures VALUES (N'The sample records from the brief are missing or altered.');

IF EXISTS (SELECT 1 FROM @Failures)
BEGIN
    SELECT FailedRule FROM @Failures;
    DECLARE @Message NVARCHAR(2048) = CONCAT(N'SAMPLE_DATA_INVALID|', (SELECT COUNT(*) FROM @Failures), N' sample-data rule(s) failed.');
    THROW 50100, @Message, 1;
END;

PRINT 'Sample data verification passed.';
GO
