/*
    Creates a ship (US-03). Administrator only.
    Errors: 50001 validation (code format, name, unknown fiscal year code or status), 50003 duplicate code.
*/
CREATE OR ALTER PROCEDURE app.usp_Ship_Create
    @ShipCode           VARCHAR(20),
    @ShipName           NVARCHAR(200),
    @FiscalYearCode     VARCHAR(10),
    @StatusName         VARCHAR(30) = NULL,
    @RequestedByUserId  INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    SET @ShipCode = UPPER(LTRIM(RTRIM(@ShipCode)));
    SET @ShipName = LTRIM(RTRIM(@ShipName));
    SET @FiscalYearCode = LTRIM(RTRIM(@FiscalYearCode));
    SET @StatusName = COALESCE(NULLIF(LTRIM(RTRIM(@StatusName)), ''), 'Active');

    IF @ShipCode IS NULL OR LEN(@ShipCode) NOT BETWEEN 3 AND 10
       OR @ShipCode COLLATE Latin1_General_BIN2 LIKE '%[^A-Z0-9]%'
        THROW 50001, N'VALIDATION_FAILED|shipCode must be 3 to 10 letters or digits.', 1;
    IF @ShipName IS NULL OR LEN(@ShipName) = 0 OR LEN(@ShipName) > 100
        THROW 50001, N'VALIDATION_FAILED|shipName is required and must be at most 100 characters.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FiscalYear WHERE FiscalYearCode = @FiscalYearCode)
        THROW 50001, N'VALIDATION_FAILED|fiscalYearCode is not a valid fiscal year code.', 1;

    DECLARE @ShipStatusId TINYINT = (SELECT ShipStatusId FROM dbo.ShipStatus WHERE StatusName = @StatusName);
    IF @ShipStatusId IS NULL
        THROW 50001, N'VALIDATION_FAILED|status must be an existing ship status.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.Ship WITH (UPDLOCK, HOLDLOCK) WHERE ShipCode = @ShipCode)
        BEGIN
            DECLARE @Conflict NVARCHAR(2048) = CONCAT(N'SHIP_CODE_CONFLICT|Ship code ''', @ShipCode, N''' already exists.');
            THROW 50003, @Conflict, 1;
        END;

        INSERT INTO dbo.Ship (ShipCode, ShipName, FiscalYearCode, ShipStatusId)
        VALUES (@ShipCode, @ShipName, @FiscalYearCode, @ShipStatusId);

        INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
        VALUES (@RequestedByUserId, 'SHIP_CREATED', 'Ship', @ShipCode,
                CONCAT(N'fiscalYear=', @FiscalYearCode, N'; status=', @StatusName));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT s.ShipCode, s.ShipName, s.FiscalYearCode, Status = st.StatusName, s.CreatedAtUtc, s.UpdatedAtUtc, RowVersion = s.RowVer
    FROM dbo.Ship AS s
    INNER JOIN dbo.ShipStatus AS st ON st.ShipStatusId = s.ShipStatusId
    WHERE s.ShipCode = @ShipCode;
END;
GO
