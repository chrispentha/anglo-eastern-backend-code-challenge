/*
    03_core_tables.sql
    Ships, crew, crew service history, application users, ship assignments, API keys, audit log.
    Conventions: surrogate INT IDENTITY keys for joins, business keys UNIQUE and used at the API boundary,
    NVARCHAR for human text, VARCHAR for codes, DATE for business dates, DATETIME2(3) UTC for audit.
*/

/* ---------- Ship ---------- */
IF OBJECT_ID(N'dbo.Ship', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ship
    (
        ShipId          INT IDENTITY(1, 1) NOT NULL,
        ShipCode        VARCHAR(10)        NOT NULL,
        ShipName        NVARCHAR(100)      NOT NULL,
        FiscalYearCode  CHAR(4)            NOT NULL,
        ShipStatusId    TINYINT            NOT NULL CONSTRAINT DF_Ship_ShipStatusId DEFAULT (1),
        CreatedAtUtc    DATETIME2(3)       NOT NULL CONSTRAINT DF_Ship_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc    DATETIME2(3)       NOT NULL CONSTRAINT DF_Ship_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        RowVer          ROWVERSION         NOT NULL,
        CONSTRAINT PK_Ship PRIMARY KEY CLUSTERED (ShipId),
        CONSTRAINT UQ_Ship_ShipCode UNIQUE (ShipCode),
        -- D-12: 3-10 upper-case letters/digits; binary collation so the range is exact.
        CONSTRAINT CK_Ship_ShipCode CHECK (
            LEN(ShipCode) BETWEEN 3 AND 10
            AND ShipCode COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%'),
        CONSTRAINT CK_Ship_ShipName CHECK (LEN(LTRIM(RTRIM(ShipName))) > 0),
        CONSTRAINT FK_Ship_FiscalYear FOREIGN KEY (FiscalYearCode) REFERENCES dbo.FiscalYear (FiscalYearCode),
        CONSTRAINT FK_Ship_ShipStatus FOREIGN KEY (ShipStatusId) REFERENCES dbo.ShipStatus (ShipStatusId)
    );
    CREATE INDEX IX_Ship_FiscalYearCode ON dbo.Ship (FiscalYearCode);
    CREATE INDEX IX_Ship_ShipStatusId ON dbo.Ship (ShipStatusId);
END;
GO

/* ---------- Crew member (personal data kept to the minimum the brief requires, SEC-14) ---------- */
IF OBJECT_ID(N'dbo.CrewMember', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrewMember
    (
        CrewMemberId     VARCHAR(20)    NOT NULL,
        FirstName        NVARCHAR(100)  NOT NULL,
        LastName         NVARCHAR(100)  NOT NULL,
        BirthDate        DATE           NOT NULL,
        NationalityCode  CHAR(2)        NOT NULL,
        CreatedAtUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_CrewMember_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_CrewMember_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        RowVer           ROWVERSION     NOT NULL,
        CONSTRAINT PK_CrewMember PRIMARY KEY CLUSTERED (CrewMemberId),
        -- D-12: 3-20 upper-case letters/digits, e.g. CREW001.
        CONSTRAINT CK_CrewMember_CrewMemberId CHECK (
            LEN(CrewMemberId) BETWEEN 3 AND 20
            AND CrewMemberId COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%'),
        CONSTRAINT CK_CrewMember_FirstName CHECK (LEN(LTRIM(RTRIM(FirstName))) > 0),
        CONSTRAINT CK_CrewMember_LastName CHECK (LEN(LTRIM(RTRIM(LastName))) > 0),
        CONSTRAINT CK_CrewMember_BirthDate CHECK (BirthDate >= '1900-01-01'),
        CONSTRAINT FK_CrewMember_Country FOREIGN KEY (NationalityCode) REFERENCES dbo.Country (CountryCode)
    );
    CREATE INDEX IX_CrewMember_NationalityCode ON dbo.CrewMember (NationalityCode);
END;
GO

/*
    ---------- Crew service history ----------
    One row per contract on a ship. Rank lives here (not on CrewMember) because rank is per contract:
    seafarers get promoted between contracts. No "one person per rank" constraint: handovers overlap (D-03).
    Status (Onboard / Planned / Relief Due / Signed Off) is derived from the dates, never stored (D-01).
*/
IF OBJECT_ID(N'dbo.CrewServiceHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrewServiceHistory
    (
        CrewServiceId      INT IDENTITY(1, 1) NOT NULL,
        CrewMemberId       VARCHAR(20)        NOT NULL,
        ShipId             INT                NOT NULL,
        RankId             SMALLINT           NOT NULL,
        SignOnDate         DATE               NOT NULL,
        SignOffDate        DATE               NULL,
        EndOfContractDate  DATE               NOT NULL,
        CreatedAtUtc       DATETIME2(3)       NOT NULL CONSTRAINT DF_CrewServiceHistory_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc       DATETIME2(3)       NOT NULL CONSTRAINT DF_CrewServiceHistory_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        RowVer             ROWVERSION         NOT NULL,
        CONSTRAINT PK_CrewServiceHistory PRIMARY KEY CLUSTERED (CrewServiceId),
        CONSTRAINT CK_CrewServiceHistory_SignOff CHECK (SignOffDate IS NULL OR SignOffDate >= SignOnDate),
        CONSTRAINT CK_CrewServiceHistory_EndOfContract CHECK (EndOfContractDate >= SignOnDate),
        CONSTRAINT FK_CrewServiceHistory_CrewMember FOREIGN KEY (CrewMemberId) REFERENCES dbo.CrewMember (CrewMemberId),
        CONSTRAINT FK_CrewServiceHistory_Ship FOREIGN KEY (ShipId) REFERENCES dbo.Ship (ShipId),
        CONSTRAINT FK_CrewServiceHistory_CrewRank FOREIGN KEY (RankId) REFERENCES dbo.CrewRank (RankId)
    );
    -- Covers exactly the crew-list population (not signed off) per ship.
    CREATE INDEX IX_CrewServiceHistory_Ship_Open
        ON dbo.CrewServiceHistory (ShipId, SignOnDate)
        INCLUDE (CrewMemberId, RankId, EndOfContractDate)
        WHERE SignOffDate IS NULL;
    CREATE INDEX IX_CrewServiceHistory_CrewMemberId ON dbo.CrewServiceHistory (CrewMemberId);
    CREATE INDEX IX_CrewServiceHistory_RankId ON dbo.CrewServiceHistory (RankId);
END;
GO

/* ---------- Application user ---------- */
IF OBJECT_ID(N'dbo.AppUser', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUser
    (
        UserId        INT IDENTITY(1, 1) NOT NULL,
        FullName      NVARCHAR(100)      NOT NULL,
        Email         NVARCHAR(254)      NULL,
        RoleId        TINYINT            NOT NULL,
        IsActive      BIT                NOT NULL CONSTRAINT DF_AppUser_IsActive DEFAULT (1),
        CreatedAtUtc  DATETIME2(3)       NOT NULL CONSTRAINT DF_AppUser_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc  DATETIME2(3)       NOT NULL CONSTRAINT DF_AppUser_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        RowVer        ROWVERSION         NOT NULL,
        CONSTRAINT PK_AppUser PRIMARY KEY CLUSTERED (UserId),
        CONSTRAINT CK_AppUser_FullName CHECK (LEN(LTRIM(RTRIM(FullName))) > 0),
        CONSTRAINT CK_AppUser_Email CHECK (Email IS NULL OR Email LIKE N'%_@_%._%'),
        CONSTRAINT FK_AppUser_AppRole FOREIGN KEY (RoleId) REFERENCES dbo.AppRole (RoleId)
    );
    -- Email is optional but unique when supplied.
    CREATE UNIQUE INDEX UX_AppUser_Email ON dbo.AppUser (Email) WHERE Email IS NOT NULL;
    CREATE INDEX IX_AppUser_RoleId ON dbo.AppUser (RoleId);
    CREATE INDEX IX_AppUser_FullName ON dbo.AppUser (FullName);
END;
GO

/* ---------- User <-> Ship assignment (many-to-many) ---------- */
IF OBJECT_ID(N'dbo.UserShip', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserShip
    (
        UserId            INT           NOT NULL,
        ShipId            INT           NOT NULL,
        AssignedAtUtc     DATETIME2(3)  NOT NULL CONSTRAINT DF_UserShip_AssignedAtUtc DEFAULT (SYSUTCDATETIME()),
        AssignedByUserId  INT           NULL,
        CONSTRAINT PK_UserShip PRIMARY KEY CLUSTERED (UserId, ShipId),
        CONSTRAINT FK_UserShip_AppUser FOREIGN KEY (UserId) REFERENCES dbo.AppUser (UserId),
        CONSTRAINT FK_UserShip_Ship FOREIGN KEY (ShipId) REFERENCES dbo.Ship (ShipId),
        CONSTRAINT FK_UserShip_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES dbo.AppUser (UserId)
    );
    CREATE INDEX IX_UserShip_ShipId_UserId ON dbo.UserShip (ShipId, UserId);
    CREATE INDEX IX_UserShip_AssignedByUserId ON dbo.UserShip (AssignedByUserId);
END;
GO

/*
    ---------- API keys (SEC-03) ----------
    Only the SHA-256 hash of the key is stored. Keys have 256 bits of CSPRNG entropy, so a fast hash is
    appropriate (no brute-force surface). KeyPrefix is non-secret and identifies a key in logs/UI.
*/
IF OBJECT_ID(N'dbo.ApiKey', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiKey
    (
        ApiKeyId         INT IDENTITY(1, 1) NOT NULL,
        UserId           INT                NOT NULL,
        KeyPrefix        CHAR(8)            NOT NULL,
        KeyHash          BINARY(32)         NOT NULL,
        CreatedAtUtc     DATETIME2(3)       NOT NULL CONSTRAINT DF_ApiKey_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ExpiresAtUtc     DATETIME2(3)       NULL,
        RevokedAtUtc     DATETIME2(3)       NULL,
        LastUsedAtUtc    DATETIME2(3)       NULL,
        CreatedByUserId  INT                NULL,
        CONSTRAINT PK_ApiKey PRIMARY KEY CLUSTERED (ApiKeyId),
        CONSTRAINT UQ_ApiKey_KeyHash UNIQUE (KeyHash),
        CONSTRAINT CK_ApiKey_KeyPrefix CHECK (KeyPrefix COLLATE Latin1_General_BIN2 NOT LIKE '%[^a-z0-9]%'),
        CONSTRAINT CK_ApiKey_Expiry CHECK (ExpiresAtUtc IS NULL OR ExpiresAtUtc > CreatedAtUtc),
        CONSTRAINT FK_ApiKey_AppUser FOREIGN KEY (UserId) REFERENCES dbo.AppUser (UserId),
        CONSTRAINT FK_ApiKey_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AppUser (UserId)
    );
    CREATE INDEX IX_ApiKey_UserId ON dbo.ApiKey (UserId);
    CREATE INDEX IX_ApiKey_CreatedByUserId ON dbo.ApiKey (CreatedByUserId);
END;
GO

/*
    ---------- Audit log (SEC-09, US-10) ----------
    Append-only record of who changed what. Written by the write procedures inside their own transaction,
    so a change and its audit row commit or roll back together. Never contains crew PII or secrets.
*/
IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog
    (
        AuditLogId     BIGINT IDENTITY(1, 1) NOT NULL,
        OccurredAtUtc  DATETIME2(3)          NOT NULL CONSTRAINT DF_AuditLog_OccurredAtUtc DEFAULT (SYSUTCDATETIME()),
        ActorUserId    INT                   NULL,
        Action         VARCHAR(50)           NOT NULL,
        EntityType     VARCHAR(30)           NOT NULL,
        EntityKey      NVARCHAR(50)          NOT NULL,
        Details        NVARCHAR(400)         NULL,
        CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (AuditLogId),
        CONSTRAINT FK_AuditLog_AppUser FOREIGN KEY (ActorUserId) REFERENCES dbo.AppUser (UserId)
    );
    CREATE INDEX IX_AuditLog_OccurredAtUtc ON dbo.AuditLog (OccurredAtUtc);
    CREATE INDEX IX_AuditLog_ActorUserId ON dbo.AuditLog (ActorUserId);
END;
GO

/* Tamper protection: audit rows can never be updated or deleted, even by a privileged session. */
CREATE OR ALTER TRIGGER dbo.TR_AuditLog_PreventChange
ON dbo.AuditLog
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50099, N'AUDIT_IMMUTABLE|Audit log rows cannot be modified or deleted.', 1;
END;
GO
