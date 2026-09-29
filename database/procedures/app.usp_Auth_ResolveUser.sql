/*
    Resolves a user id (the 'sub' claim of a validated JWT) to the caller's security context.
    Same result sets as app.usp_Auth_ResolveApiKey; empty when the user is unknown or inactive.
*/
CREATE OR ALTER PROCEDURE app.usp_Auth_ResolveUser
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT uc.UserId, uc.RoleName, uc.IsAdministrator
    FROM dbo.tvf_UserContext(@UserId) AS uc;

    SELECT s.ShipCode
    FROM dbo.UserShip AS us
    INNER JOIN dbo.Ship AS s ON s.ShipId = us.ShipId
    INNER JOIN dbo.tvf_UserContext(@UserId) AS uc ON uc.UserId = us.UserId
    ORDER BY s.ShipCode;
END;
GO
