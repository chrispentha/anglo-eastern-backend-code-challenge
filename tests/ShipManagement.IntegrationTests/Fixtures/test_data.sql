/*
    Deterministic fixture data for the integration tests, loaded (as sa) after database/deploy.sh.
    Every date is fixed; tests that depend on "today" pin @AsOfDate = '2025-06-15'.
    Test ships TST01..TST03 are separate from the sample ships, so sample data changes never break tests.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO dbo.Ship (ShipCode, ShipName, FiscalYearCode, ShipStatusId)
VALUES ('TST01', N'Test Vessel One',   '0112', 1),
       ('TST02', N'Test Vessel Two',   '0403', 1),
       ('TST03', N'Test Vessel Three', '0112', 2);

/* ---------- Users and API keys ---------- */
INSERT INTO dbo.AppUser (FullName, Email, RoleId, IsActive)
SELECT v.FullName, v.Email, r.RoleId, v.IsActive
FROM (VALUES
        (N'Test Admin',     N'test.admin@example.test',     'Administrator',  1),
        (N'Test Crewing',   N'test.crewing@example.test',   'CrewingOfficer', 1),
        (N'Test Other',     N'test.other@example.test',     'Accountant',     1),
        (N'Test Inactive',  N'test.inactive@example.test',  'CrewingOfficer', 0),
        (N'Test RateLimit', N'test.ratelimit@example.test', 'Accountant',     1)
     ) AS v (FullName, Email, RoleName, IsActive)
JOIN dbo.AppRole AS r ON r.RoleName = v.RoleName;

INSERT INTO dbo.UserShip (UserId, ShipId)
SELECT u.UserId, s.ShipId
FROM (VALUES (N'test.crewing@example.test', 'TST01'), (N'test.crewing@example.test', 'TST02'),
             (N'test.crewing@example.test', 'TST03'), (N'test.inactive@example.test', 'TST01'),
             (N'test.ratelimit@example.test', 'TST01')) AS v (Email, ShipCode)
JOIN dbo.AppUser AS u ON u.Email = v.Email
JOIN dbo.Ship AS s ON s.ShipCode = v.ShipCode;

INSERT INTO dbo.ApiKey (UserId, KeyPrefix, KeyHash, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc)
SELECT u.UserId, v.KeyPrefix,
       HASHBYTES('SHA2_256', CAST('sm_' + v.KeyPrefix + '_' + REPLICATE(v.Fill, 43) AS VARCHAR(100))),
       v.CreatedAtUtc, v.ExpiresAtUtc, v.RevokedAtUtc
FROM (VALUES
        (N'test.admin@example.test',     'tstadm01', 'A', SYSUTCDATETIME(), CAST(NULL AS DATETIME2(3)), CAST(NULL AS DATETIME2(3))),
        (N'test.crewing@example.test',   'tstcrw01', 'B', SYSUTCDATETIME(), NULL, NULL),
        (N'test.other@example.test',     'tstoth01', 'C', SYSUTCDATETIME(), NULL, NULL),
        (N'test.inactive@example.test',  'tstina01', 'D', SYSUTCDATETIME(), NULL, NULL),
        (N'test.crewing@example.test',   'tstrev01', 'E', SYSUTCDATETIME(), NULL, SYSUTCDATETIME()),
        (N'test.crewing@example.test',   'tstexp01', 'F', '2020-01-01',     '2020-02-01', NULL),
        (N'test.ratelimit@example.test', 'tstrat01', 'G', SYSUTCDATETIME(), NULL, NULL)
     ) AS v (Email, KeyPrefix, Fill, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc)
JOIN dbo.AppUser AS u ON u.Email = v.Email;

/* ---------- Crew (as of 2025-06-15) ---------- */
INSERT INTO dbo.CrewMember (CrewMemberId, FirstName, LastName, BirthDate, NationalityCode)
VALUES ('TC001', N'Andreas',  N'Philip',       '1975-03-10', 'GR'),
       ('TC002', N'Nikos',    N'Philippou',    '1978-09-01', 'GR'),
       ('TC003', N'Maria',    N'Santos',       '1985-01-20', 'PH'),
       ('TC004', N'Jan',      N'Kowalski',     '1990-11-11', 'PL'),
       ('TC005', N'Ivan',     N'Horvat',       '1982-05-05', 'HR'),
       ('TC006', N'Wei',      N'Liu',          '1991-07-07', 'CN'),
       ('TC007', N'Juan',     N'Cruz',         '1990-06-15', 'PH'),
       ('TC008', N'Jose',     N'Reyes',        '1990-06-16', 'PH'),
       ('TC009', N'Leap',     N'Year',         '2000-02-29', 'GB'),
       ('TC010', N'Γιώργος',  N'Παπαδόπουλος', '1988-12-12', 'GR'),
       ('TC011', N'Sam',      N'Under_Score',  '1993-03-03', 'GB'),
       ('TC012', N'Aung',     N'Maras',        '1995-04-04', 'MM'),
       ('TC013', N'Kyaw',     N'Oo',           '1996-08-08', 'MM'),
       ('TC014', N'Idle',     N'Sailor',       '1980-01-01', 'NO');

INSERT INTO dbo.CrewServiceHistory (CrewMemberId, ShipId, RankId, SignOnDate, EndOfContractDate, SignOffDate)
SELECT v.CrewMemberId, s.ShipId, r.RankId, v.SignOnDate, v.EndOfContractDate, v.SignOffDate
FROM (VALUES
        -- id      ship     rank    sign on       end of contract  sign off        status at 2025-06-15
        ('TC001', 'TST01', 'MST',  '2025-01-10', '2025-05-16', NULL),         -- Onboard: exactly 30 days past EOC
        ('TC002', 'TST01', 'CE',   '2025-01-10', '2025-05-15', NULL),         -- Relief Due: 31 days past EOC
        ('TC003', 'TST01', 'CO',   '2025-06-15', '2025-12-15', NULL),         -- Onboard: signs on today
        ('TC004', 'TST01', '2O',   '2025-06-16', '2025-12-16', NULL),         -- Planned: signs on tomorrow
        ('TC005', 'TST01', '2E',   '2025-01-01', '2025-06-30', '2025-05-01'), -- Signed Off (past)
        ('TC006', 'TST01', '3O',   '2025-03-01', '2025-09-01', '2025-09-01'), -- Signed Off (future sign-off date set)
        ('TC007', 'TST01', 'AB',   '2025-04-05', '2025-10-05', NULL),         -- Onboard, birthday today (35)
        ('TC008', 'TST01', 'AB',   '2025-04-05', '2025-10-05', NULL),         -- Onboard, birthday tomorrow (34)
        ('TC009', 'TST01', 'OLR',  '2025-02-01', '2025-08-01', NULL),         -- Onboard, born 29 Feb
        ('TC010', 'TST01', 'WPR',  '2025-02-10', '2025-08-10', NULL),         -- Onboard, Greek script
        ('TC011', 'TST01', 'BSN',  '2025-03-03', '2025-09-03', NULL),         -- Onboard, underscore in name
        ('TC012', 'TST01', 'MSM',  '2024-12-20', '2025-06-20', NULL),         -- Onboard
        ('TC013', 'TST01', 'OS',   '2024-11-01', '2025-03-01', NULL),         -- Relief Due
        ('TC014', 'TST03', 'AB',   '2025-01-01', '2025-12-01', NULL)          -- on an inactive ship
     ) AS v (CrewMemberId, ShipCode, RankCode, SignOnDate, EndOfContractDate, SignOffDate)
JOIN dbo.Ship AS s ON s.ShipCode = v.ShipCode
JOIN dbo.CrewRank AS r ON r.RankCode = v.RankCode;

/* ---------- Finance ---------- */
-- TST02, fiscal year 0403. Report period 2025-02 -> YTD Apr 2024 .. Feb 2025.
INSERT INTO dbo.BudgetEntry (ShipId, AccountId, AccountPeriod, BudgetAmount)
SELECT s.ShipId, a.AccountId, v.AccountPeriod, v.Amount
FROM (VALUES
        ('TST02', '7110000', '2024-04-01', 100),
        ('TST02', '7110000', '2024-10-01', 200),
        ('TST02', '7110000', '2025-02-01', 300),
        ('TST02', '7110000', '2025-04-01', 50),    -- next fiscal year: only in the Apr 2025 report
        ('TST02', '7120000', '2025-02-01', 0),     -- explicit zero only: excluded from reports
        ('TST02', '7135000', '2025-01-01', 0),     -- two budget lines in one period: summed
        ('TST02', '7135000', '2025-01-01', 1000),
        ('TST02', '7210000', '2025-02-01', 5000),  -- budget, no actuals
        ('TST01', '7110000', '2024-12-01', 100),   -- previous fiscal year for 0112
        ('TST01', '7110000', '2025-01-01', 100),
        ('TST01', '7110000', '2025-02-01', 100),
        ('TST01', '7110000', '2025-03-01', 100),
        ('TST01', '7110000', '2025-04-01', 100),
        ('TST01', '7110000', '2025-05-01', 100),
        ('TST01', '7110000', '2025-06-01', 100),
        ('TST01', '7110000', '2025-07-01', 100)
     ) AS v (ShipCode, AccountNumber, AccountPeriod, Amount)
JOIN dbo.Ship AS s ON s.ShipCode = v.ShipCode
JOIN dbo.Account AS a ON a.AccountNumber = v.AccountNumber;

INSERT INTO dbo.AccountTransaction (ShipId, AccountId, AccountPeriod, ActualAmount)
SELECT s.ShipId, a.AccountId, v.AccountPeriod, v.Amount
FROM (VALUES
        ('TST02', '7110000', '2024-03-01', 999),   -- previous fiscal year: never in the 2025-02 YTD
        ('TST02', '7110000', '2025-02-01', 250),   -- two transactions in the period: summed
        ('TST02', '7110000', '2025-02-01', 50),
        ('TST02', '7110000', '2025-03-01', 777),   -- after the period: never included
        ('TST02', '7120000', '2025-02-01', 0),     -- zero only: excluded
        ('TST02', '7135000', '2025-01-01', 300),   -- the brief's 300 + 0 + 700 = 1000
        ('TST02', '7135000', '2025-01-01', 0),
        ('TST02', '7135000', '2025-01-01', 700),
        ('TST02', '7135000', '2025-02-01', 0),     -- zero in the period: 0, not NULL
        ('TST02', '7140000', '2025-02-01', 400),   -- actual without any budget
        ('TST01', '7110000', '2025-07-01', 80)
     ) AS v (ShipCode, AccountNumber, AccountPeriod, Amount)
JOIN dbo.Ship AS s ON s.ShipCode = v.ShipCode
JOIN dbo.Account AS a ON a.AccountNumber = v.AccountNumber;

COMMIT TRANSACTION;
