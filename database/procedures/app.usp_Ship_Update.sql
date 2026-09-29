/*
    Renames a ship and/or changes its status, e.g. deactivation (D-20: ships are deactivated, never deleted).
    The fiscal year is immutable because changing it would restate historical reports (D-23). Administrator only.

    Optimistic concurrency (D-30): when @ExpectedRowVersion is supplied (the API's If-Match header), the update
    only succeeds if the ship has not changed since the caller read it; otherwise 50006 (HTTP 412), so two
    administrators editing the same ship can never silently overwrite each other.
*/
CREATE OR ALTER PROCEDURE app.usp_Ship_Update
    @ShipCode           VARCHAR(20),
    @ShipName           NVARCHAR(200) = NULL,
    @StatusName         VARCHAR(30) = NULL,
    @RequestedByUserId  INT,
    @ExpectedRowVersion BINARY(8) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    SET @ShipCode = UPPER(LTRIM(RTRIM(@ShipCode)));
    SET @ShipName = LTRIM(RTRIM(@ShipName));
    SET @StatusName = NULLIF(LTRIM(RTRIM(@StatusName)), '');

    IF @ShipName IS NULL AND @StatusName IS NULL
        THROW 50001, N'VALIDATION_FAILED|At least one of shipName or status must be provided.', 1;
    IF @ShipName IS NOT NULL AND (LEN(@ShipName) = 0 OR LEN(@ShipName) > 100)
        THROW 50001, N'VALIDATION_FAILED|shipName must be 1 to 100 characters.', 1;

    DECLARE @ShipStatusId TINYINT = NULL;
    IF @StatusName IS NOT NULL
    BEGIN
        SET @ShipStatusId = (SELECT ShipStatusId FROM dbo.ShipStatus WHERE StatusName = @StatusName);
        IF @ShipStatusId IS NULL
            THROW 50001, N'VALIDATION_FAILED|status must be an existing ship status.', 1;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @ShipId INT, @OldName NVARCHAR(100), @OldStatus VARCHAR(20), @CurrentRowVersion BINARY(8);

        SELECT @ShipId = s.ShipId, @OldName = s.ShipName, @OldStatus = st.StatusName, @CurrentRowVersion = s.RowVer
        FROM dbo.Ship AS s WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.ShipStatus AS st ON st.ShipStatusId = s.ShipStatusId
        WHERE s.ShipCode = @ShipCode;

        IF @ShipId IS NULL
        BEGIN
            DECLARE @NotFound NVARCHAR(2048) = CONCAT(N'SHIP_NOT_FOUND|Ship ''', @ShipCode, N''' was not found.');
            THROW 50002, @NotFound, 1;
        END;

        -- The row is locked (UPDLOCK, HOLDLOCK), so no other writer can change it between this check and the update.
        IF @ExpectedRowVersion IS NOT NULL AND @ExpectedRowVersion <> @CurrentRowVersion
        BEGIN
            DECLARE @Stale NVARCHAR(2048) = CONCAT(N'PRECONDITION_FAILED|Ship ''', @ShipCode,
                N''' was modified by someone else. Read it again and retry with the new ETag.');
            THROW 50006, @Stale, 1;
        END;

        UPDATE dbo.Ship
        SET ShipName = COALESCE(@ShipName, ShipName),
            ShipStatusId = COALESCE(@ShipStatusId, ShipStatusId),
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE ShipId = @ShipId;

        INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
        VALUES (@RequestedByUserId, 'SHIP_UPDATED', 'Ship', @ShipCode,
                CONCAT(N'status: ', @OldStatus, N' -> ', COALESCE(@StatusName, @OldStatus),
                       CASE WHEN @ShipName IS NOT NULL AND @ShipName <> @OldName THEN N'; renamed' ELSE N'' END));

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
