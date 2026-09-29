/*
    02_reference_tables.sql
    Lookup tables. Rows are seeded by 07_seed_reference.sql; nothing here is hardcoded in procedures.
    Re-runnable: each table is created only if missing.
*/

/* Fiscal year codes 'MMNN': MM = first month, NN = last month (D-11). */
IF OBJECT_ID(N'dbo.FiscalYear', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FiscalYear
    (
        FiscalYearCode  CHAR(4)       NOT NULL,
        StartMonth      TINYINT       NOT NULL,
        EndMonth        TINYINT       NOT NULL,
        Description     NVARCHAR(50)  NOT NULL,
        CONSTRAINT PK_FiscalYear PRIMARY KEY CLUSTERED (FiscalYearCode),
        CONSTRAINT CK_FiscalYear_StartMonth CHECK (StartMonth BETWEEN 1 AND 12),
        -- A fiscal year is always 12 consecutive months: the end month is the month before the start month.
        CONSTRAINT CK_FiscalYear_EndMonth CHECK (EndMonth = (StartMonth + 10) % 12 + 1),
        CONSTRAINT CK_FiscalYear_Code CHECK (
            FiscalYearCode = RIGHT('0' + CAST(StartMonth AS VARCHAR(2)), 2)
                           + RIGHT('0' + CAST(EndMonth   AS VARCHAR(2)), 2))
    );
END;
GO

/* Lookup instead of a BIT so statuses such as 'Laid-up' can be added later without schema change. */
IF OBJECT_ID(N'dbo.ShipStatus', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShipStatus
    (
        ShipStatusId    TINYINT      NOT NULL,
        StatusName      VARCHAR(20)  NOT NULL,
        IsOperational   BIT          NOT NULL,  -- only operational ships appear in crew lists and reports
        CONSTRAINT PK_ShipStatus PRIMARY KEY CLUSTERED (ShipStatusId),
        CONSTRAINT UQ_ShipStatus_StatusName UNIQUE (StatusName)
    );
END;
GO

IF OBJECT_ID(N'dbo.Department', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Department
    (
        DepartmentId    TINYINT      NOT NULL,
        DepartmentName  VARCHAR(20)  NOT NULL,
        CONSTRAINT PK_Department PRIMARY KEY CLUSTERED (DepartmentId),
        CONSTRAINT UQ_Department_DepartmentName UNIQUE (DepartmentName)
    );
END;
GO

/* Maritime ranks (STCW structure). SeniorityOrder drives "sort by rank" (D-15): 1 = Master. */
IF OBJECT_ID(N'dbo.CrewRank', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrewRank
    (
        RankId          SMALLINT      NOT NULL,
        RankCode        VARCHAR(10)   NOT NULL,
        RankName        NVARCHAR(50)  NOT NULL,
        DepartmentId    TINYINT       NOT NULL,
        SeniorityOrder  SMALLINT      NOT NULL,
        CONSTRAINT PK_CrewRank PRIMARY KEY CLUSTERED (RankId),
        CONSTRAINT UQ_CrewRank_RankCode UNIQUE (RankCode),
        CONSTRAINT UQ_CrewRank_RankName UNIQUE (RankName),
        CONSTRAINT UQ_CrewRank_SeniorityOrder UNIQUE (SeniorityOrder),
        CONSTRAINT CK_CrewRank_SeniorityOrder CHECK (SeniorityOrder > 0),
        CONSTRAINT FK_CrewRank_Department FOREIGN KEY (DepartmentId) REFERENCES dbo.Department (DepartmentId)
    );
    CREATE INDEX IX_CrewRank_DepartmentId ON dbo.CrewRank (DepartmentId);
END;
GO

/* ISO 3166-1 alpha-2 countries; NationalityName is what the crew list displays ("Greek"). */
IF OBJECT_ID(N'dbo.Country', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Country
    (
        CountryCode      CHAR(2)        NOT NULL,
        CountryName      NVARCHAR(100)  NOT NULL,
        NationalityName  NVARCHAR(50)   NOT NULL,
        CONSTRAINT PK_Country PRIMARY KEY CLUSTERED (CountryCode),
        CONSTRAINT CK_Country_CountryCode CHECK (CountryCode COLLATE Latin1_General_BIN2 LIKE '[A-Z][A-Z]')
    );
END;
GO

/* Application roles (D-18). IsAdministrator is the flag procedures check, so no role name is hardcoded. */
IF OBJECT_ID(N'dbo.AppRole', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppRole
    (
        RoleId           TINYINT        NOT NULL,
        RoleName         VARCHAR(30)    NOT NULL,
        Description      NVARCHAR(200)  NOT NULL,
        IsAdministrator  BIT            NOT NULL,
        CONSTRAINT PK_AppRole PRIMARY KEY CLUSTERED (RoleId),
        CONSTRAINT UQ_AppRole_RoleName UNIQUE (RoleName)
    );
END;
GO
