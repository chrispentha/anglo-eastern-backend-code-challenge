/*
    Assigns a ship to a user (US-05). Idempotent: assigning an already-assigned ship is not an error;
    @Created tells the caller whether a new assignment was made. Administrator only.
    Errors: 50002 unknown user or ship.
*/
CREATE OR ALTER PROCEDURE app.usp_UserShip_Assign
    @UserId             INT,
    @ShipCode           VARCHAR(20),
    @RequestedByUserId  INT,
    @Created            BIT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    SET @ShipCode = UPPER(LTRIM(RTRIM(@ShipCode)));
    SET @Created = 0;

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

        -- UPDLOCK + HOLDLOCK serialises concurrent assigns of the same pair: no duplicate-key race.
        IF NOT EXISTS (SELECT 1 FROM dbo.UserShip WITH (UPDLOCK, HOLDLOCK) WHERE UserId = @UserId AND ShipId = @ShipId)
        BEGIN
            INSERT INTO dbo.UserShip (UserId, ShipId, AssignedByUserId)
            VALUES (@UserId, @ShipId, @RequestedByUserId);

            INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
            VALUES (@RequestedByUserId, 'SHIP_ASSIGNED', 'UserShip',
                    CONCAT(@UserId, N'/', @ShipCode), NULL);

            SET @Created = 1;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
