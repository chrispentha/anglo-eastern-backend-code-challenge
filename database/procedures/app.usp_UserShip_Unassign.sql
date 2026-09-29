/*
    Removes a ship assignment. Idempotent: removing a non-existent assignment is not an error;
    @Removed tells the caller whether anything changed. Administrator only.
    Errors: 50002 unknown user or ship.
*/
CREATE OR ALTER PROCEDURE app.usp_UserShip_Unassign
    @UserId             INT,
    @ShipCode           VARCHAR(20),
    @RequestedByUserId  INT,
    @Removed            BIT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    SET @ShipCode = UPPER(LTRIM(RTRIM(@ShipCode)));
    SET @Removed = 0;

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE UserId = @UserId)
        THROW 50002, N'USER_NOT_FOUND|The user was not found.', 1;

    DECLARE @ShipId INT = (SELECT ShipId FROM dbo.Ship WHERE ShipCode = @ShipCode);
    IF @ShipId IS NULL
    BEGIN
        DECLARE @NotFound NVARCHAR(2048) = CONCAT(N'SHIP_NOT_FOUND|Ship ''', @ShipCode, N''' was not found.');
        THROW 50002, @NotFound, 1;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        DELETE FROM dbo.UserShip WHERE UserId = @UserId AND ShipId = @ShipId;

        IF @@ROWCOUNT > 0
        BEGIN
            INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
            VALUES (@RequestedByUserId, 'SHIP_UNASSIGNED', 'UserShip', CONCAT(@UserId, N'/', @ShipCode), NULL);
            SET @Removed = 1;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
