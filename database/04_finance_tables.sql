/*
    04_finance_tables.sql
    Chart of Accounts (hierarchical), budget lines and actual transactions.

    Integrity rules enforced declaratively (no triggers):
    - A parent account must be a summary account ('P')                         (D-22)
    - Budgets and actuals can only be posted to detail accounts ('C')          (D-08)
    - Amounts are known values: NOT NULL and never negative. "Unknown / no data"
      is the absence of rows, which reports surface as NULL (0 <> NULL, D-06).
    - Accounting period is the first day of a month                            (D-10)
    - Several rows per ship/account/period are allowed and are summed          (D-05)
*/

/* ---------- Chart of Accounts ---------- */
IF OBJECT_ID(N'dbo.Account', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Account
    (
        AccountId          INT IDENTITY(1, 1) NOT NULL,
        AccountNumber      VARCHAR(20)        NOT NULL,
        Description        NVARCHAR(200)      NOT NULL,
        AccountType        CHAR(1)            NOT NULL,  -- 'P' = parent (summary), 'C' = child (detail)
        ParentAccountId    INT                NULL,
        -- Constant used only by the composite FK below: whatever the parent is, it must be of type 'P'.
        ParentAccountType  AS CAST('P' AS CHAR(1)) PERSISTED,
        CreatedAtUtc       DATETIME2(3)       NOT NULL CONSTRAINT DF_Account_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Account PRIMARY KEY CLUSTERED (AccountId),
        CONSTRAINT UQ_Account_AccountNumber UNIQUE (AccountNumber),
        CONSTRAINT UQ_Account_AccountId_AccountType UNIQUE (AccountId, AccountType),
        CONSTRAINT CK_Account_AccountType CHECK (AccountType IN ('P', 'C')),
        CONSTRAINT CK_Account_AccountNumber CHECK (
            LEN(AccountNumber) > 0 AND AccountNumber COLLATE Latin1_General_BIN2 NOT LIKE '%[^0-9A-Z.-]%'),
        CONSTRAINT CK_Account_Description CHECK (LEN(LTRIM(RTRIM(Description))) > 0),
        CONSTRAINT CK_Account_NotOwnParent CHECK (ParentAccountId IS NULL OR ParentAccountId <> AccountId),
        CONSTRAINT FK_Account_Parent FOREIGN KEY (ParentAccountId, ParentAccountType)
            REFERENCES dbo.Account (AccountId, AccountType)
    );
    CREATE INDEX IX_Account_ParentAccountId ON dbo.Account (ParentAccountId);
END;
GO

/* ---------- Budget lines ---------- */
IF OBJECT_ID(N'dbo.BudgetEntry', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BudgetEntry
    (
        BudgetEntryId  BIGINT IDENTITY(1, 1) NOT NULL,
        ShipId         INT                   NOT NULL,
        AccountId      INT                   NOT NULL,
        -- Always 'C': together with the composite FK this makes posting to a parent account impossible.
        AccountType    CHAR(1)               NOT NULL CONSTRAINT DF_BudgetEntry_AccountType DEFAULT ('C'),
        AccountPeriod  DATE                  NOT NULL,
        BudgetAmount   DECIMAL(19, 2)        NOT NULL,
        CreatedAtUtc   DATETIME2(3)          NOT NULL CONSTRAINT DF_BudgetEntry_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_BudgetEntry PRIMARY KEY CLUSTERED (BudgetEntryId),
        CONSTRAINT CK_BudgetEntry_AccountType CHECK (AccountType = 'C'),
        CONSTRAINT CK_BudgetEntry_AccountPeriod CHECK (DAY(AccountPeriod) = 1 AND AccountPeriod >= '2000-01-01'),
        CONSTRAINT CK_BudgetEntry_BudgetAmount CHECK (BudgetAmount >= 0),
        CONSTRAINT FK_BudgetEntry_Ship FOREIGN KEY (ShipId) REFERENCES dbo.Ship (ShipId),
        CONSTRAINT FK_BudgetEntry_Account FOREIGN KEY (AccountId, AccountType)
            REFERENCES dbo.Account (AccountId, AccountType)
    );
    -- Year-to-date range seek per ship, covering the amount.
    CREATE INDEX IX_BudgetEntry_Ship_Period_Account
        ON dbo.BudgetEntry (ShipId, AccountPeriod, AccountId) INCLUDE (BudgetAmount);
    CREATE INDEX IX_BudgetEntry_AccountId ON dbo.BudgetEntry (AccountId, AccountType);
END;
GO

/* ---------- Actual transactions ---------- */
IF OBJECT_ID(N'dbo.AccountTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountTransaction
    (
        AccountTransactionId  BIGINT IDENTITY(1, 1) NOT NULL,
        ShipId                INT                   NOT NULL,
        AccountId             INT                   NOT NULL,
        AccountType           CHAR(1)               NOT NULL CONSTRAINT DF_AccountTransaction_AccountType DEFAULT ('C'),
        AccountPeriod         DATE                  NOT NULL,
        ActualAmount          DECIMAL(19, 2)        NOT NULL,
        TransactionDate       DATE                  NULL,
        Reference             NVARCHAR(50)          NULL,
        CreatedAtUtc          DATETIME2(3)          NOT NULL CONSTRAINT DF_AccountTransaction_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AccountTransaction PRIMARY KEY CLUSTERED (AccountTransactionId),
        CONSTRAINT CK_AccountTransaction_AccountType CHECK (AccountType = 'C'),
        CONSTRAINT CK_AccountTransaction_AccountPeriod CHECK (DAY(AccountPeriod) = 1 AND AccountPeriod >= '2000-01-01'),
        CONSTRAINT CK_AccountTransaction_ActualAmount CHECK (ActualAmount >= 0),
        CONSTRAINT FK_AccountTransaction_Ship FOREIGN KEY (ShipId) REFERENCES dbo.Ship (ShipId),
        CONSTRAINT FK_AccountTransaction_Account FOREIGN KEY (AccountId, AccountType)
            REFERENCES dbo.Account (AccountId, AccountType)
    );
    CREATE INDEX IX_AccountTransaction_Ship_Period_Account
        ON dbo.AccountTransaction (ShipId, AccountPeriod, AccountId) INCLUDE (ActualAmount);
    CREATE INDEX IX_AccountTransaction_AccountId ON dbo.AccountTransaction (AccountId, AccountType);
END;
GO
