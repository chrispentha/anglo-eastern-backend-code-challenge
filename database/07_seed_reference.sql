/*
    07_seed_reference.sql
    Reference data. Re-runnable: rows are inserted when missing and updated when changed (upsert by key).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* Fiscal years (D-11): Jan-Dec, Apr-Mar, Jul-Jun, Oct-Sep. */
WITH Src AS
(
    SELECT v.FiscalYearCode, v.StartMonth, v.EndMonth, v.Description FROM (VALUES
        ('0112', CAST(1  AS TINYINT), CAST(12 AS TINYINT), N'January - December'),
        ('0403', CAST(4  AS TINYINT), CAST(3  AS TINYINT), N'April - March'),
        ('0706', CAST(7  AS TINYINT), CAST(6  AS TINYINT), N'July - June'),
        ('1009', CAST(10 AS TINYINT), CAST(9  AS TINYINT), N'October - September')
    ) AS v (FiscalYearCode, StartMonth, EndMonth, Description)
)
INSERT INTO dbo.FiscalYear (FiscalYearCode, StartMonth, EndMonth, Description)
SELECT s.FiscalYearCode, s.StartMonth, s.EndMonth, s.Description
FROM Src AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.FiscalYear AS t WHERE t.FiscalYearCode = s.FiscalYearCode);

/* Ship statuses: only operational ships appear in crew lists and financial reports. */
WITH Src AS
(
    SELECT v.ShipStatusId, v.StatusName, v.IsOperational FROM (VALUES
        (CAST(1 AS TINYINT), 'Active',   CAST(1 AS BIT)),
        (CAST(2 AS TINYINT), 'Inactive', CAST(0 AS BIT))
    ) AS v (ShipStatusId, StatusName, IsOperational)
)
INSERT INTO dbo.ShipStatus (ShipStatusId, StatusName, IsOperational)
SELECT s.ShipStatusId, s.StatusName, s.IsOperational
FROM Src AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.ShipStatus AS t WHERE t.ShipStatusId = s.ShipStatusId);

/* Departments. */
WITH Src AS
(
    SELECT v.DepartmentId, v.DepartmentName FROM (VALUES
        (CAST(1 AS TINYINT), 'Deck'),
        (CAST(2 AS TINYINT), 'Engine'),
        (CAST(3 AS TINYINT), 'Catering')
    ) AS v (DepartmentId, DepartmentName)
)
INSERT INTO dbo.Department (DepartmentId, DepartmentName)
SELECT s.DepartmentId, s.DepartmentName
FROM Src AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Department AS t WHERE t.DepartmentId = s.DepartmentId);

/*
    Ranks (STCW structure). SeniorityOrder is the order a mariner expects when sorting by rank (D-15):
    senior officers first, then junior officers, cadets, ratings; Wiper last.
*/
DECLARE @Rank TABLE (RankId SMALLINT, RankCode VARCHAR(10), RankName NVARCHAR(50), DepartmentId TINYINT, SeniorityOrder SMALLINT);
INSERT INTO @Rank (RankId, RankCode, RankName, DepartmentId, SeniorityOrder)
VALUES
    (1,  'MST',  N'Master',                         1, 1),
    (2,  'CE',   N'Chief Engineer',                 2, 2),
    (3,  'CO',   N'Chief Officer',                  1, 3),
    (4,  '2E',   N'Second Engineer',                2, 4),
    (5,  '2O',   N'Second Officer',                 1, 5),
    (6,  '3E',   N'Third Engineer',                 2, 6),
    (7,  '3O',   N'Third Officer',                  1, 7),
    (8,  '4E',   N'Fourth Engineer',                2, 8),
    (9,  'ETO',  N'Electro-Technical Officer',      2, 9),
    (10, 'DCDT', N'Deck Cadet',                     1, 10),
    (11, 'ECDT', N'Engine Cadet',                   2, 11),
    (12, 'BSN',  N'Bosun',                          1, 12),
    (13, 'FTR',  N'Fitter',                         2, 13),
    (14, 'AB',   N'Able Seaman',                    1, 14),
    (15, 'OLR',  N'Oiler',                          2, 15),
    (16, 'CCK',  N'Chief Cook',                     3, 16),
    (17, 'OS',   N'Ordinary Seaman',                1, 17),
    (18, 'MSM',  N'Messman',                        3, 18),
    (19, 'WPR',  N'Wiper',                          2, 19);

UPDATE t
SET t.RankCode = s.RankCode, t.RankName = s.RankName, t.DepartmentId = s.DepartmentId, t.SeniorityOrder = s.SeniorityOrder
FROM dbo.CrewRank AS t
INNER JOIN @Rank AS s ON s.RankId = t.RankId
WHERE t.RankCode <> s.RankCode OR t.RankName <> s.RankName
   OR t.DepartmentId <> s.DepartmentId OR t.SeniorityOrder <> s.SeniorityOrder;

INSERT INTO dbo.CrewRank (RankId, RankCode, RankName, DepartmentId, SeniorityOrder)
SELECT s.RankId, s.RankCode, s.RankName, s.DepartmentId, s.SeniorityOrder
FROM @Rank AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.CrewRank AS t WHERE t.RankId = s.RankId);

/* Countries of the seafarers in the sample data (ISO 3166-1 alpha-2). */
WITH Src AS
(
    SELECT v.CountryCode, v.CountryName, v.NationalityName FROM (VALUES
        ('GR', N'Greece',          N'Greek'),
        ('PH', N'Philippines',     N'Filipino'),
        ('IN', N'India',           N'Indian'),
        ('UA', N'Ukraine',         N'Ukrainian'),
        ('PL', N'Poland',          N'Polish'),
        ('ID', N'Indonesia',       N'Indonesian'),
        ('HR', N'Croatia',         N'Croatian'),
        ('MX', N'Mexico',          N'Mexican'),
        ('RO', N'Romania',         N'Romanian'),
        ('CN', N'China',           N'Chinese'),
        ('MM', N'Myanmar',         N'Burmese'),
        ('GB', N'United Kingdom',  N'British'),
        ('NO', N'Norway',          N'Norwegian'),
        ('VN', N'Viet Nam',        N'Vietnamese'),
        ('LV', N'Latvia',          N'Latvian')
    ) AS v (CountryCode, CountryName, NationalityName)
)
INSERT INTO dbo.Country (CountryCode, CountryName, NationalityName)
SELECT s.CountryCode, s.CountryName, s.NationalityName
FROM Src AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Country AS t WHERE t.CountryCode = s.CountryCode);

/* Application roles (D-18). Only administrators manage users, ships, assignments and API keys. */
WITH Src AS
(
    SELECT v.RoleId, v.RoleName, v.Description, v.IsAdministrator FROM (VALUES
        (CAST(1 AS TINYINT), 'Administrator',       N'Manages users, ships, assignments and API keys; sees every ship.', CAST(1 AS BIT)),
        (CAST(2 AS TINYINT), 'FleetManager',        N'Oversees a group of assigned vessels.',                           CAST(0 AS BIT)),
        (CAST(3 AS TINYINT), 'Superintendent',      N'Technical superintendent for assigned vessels.',                  CAST(0 AS BIT)),
        (CAST(4 AS TINYINT), 'CrewingOfficer',      N'Manages crew rotation for assigned vessels.',                     CAST(0 AS BIT)),
        (CAST(5 AS TINYINT), 'Accountant',          N'Prepares vessel accounts for assigned vessels.',                  CAST(0 AS BIT)),
        (CAST(6 AS TINYINT), 'OwnerRepresentative', N'Read-only access to the owner''s assigned vessels.',              CAST(0 AS BIT))
    ) AS v (RoleId, RoleName, Description, IsAdministrator)
)
INSERT INTO dbo.AppRole (RoleId, RoleName, Description, IsAdministrator)
SELECT s.RoleId, s.RoleName, s.Description, s.IsAdministrator
FROM Src AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.AppRole AS t WHERE t.RoleId = s.RoleId);

COMMIT TRANSACTION;
GO
