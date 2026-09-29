/*
    Ships assigned to a user, with status (US-06). Callers may list their own ships;
    administrators may list anyone's. Errors: 50005 not allowed, 50002 user not found.
*/
CREATE OR ALTER PROCEDURE app.usp_UserShip_ListByUser
    @UserId             INT,
    @RequestedByUserId  INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId)
                   WHERE IsAdministrator = 1 OR UserId = @UserId)
        THROW 50005, N'FORBIDDEN|You are not allowed to view this user''s ships.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE UserId = @UserId)
        THROW 50002, N'USER_NOT_FOUND|The user was not found.', 1;

    SELECT s.ShipCode, s.ShipName, s.FiscalYearCode, Status = st.StatusName, us.AssignedAtUtc
    FROM dbo.UserShip AS us
    INNER JOIN dbo.Ship AS s ON s.ShipId = us.ShipId
    INNER JOIN dbo.ShipStatus AS st ON st.ShipStatusId = s.ShipStatusId
    WHERE us.UserId = @UserId
    ORDER BY s.ShipCode;
END;
GO
