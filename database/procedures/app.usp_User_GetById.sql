/*
    Returns one user. Callers may read themselves; administrators may read anyone (SEC-04).
    Errors: 50002 user not found, 50005 not allowed.
*/
CREATE OR ALTER PROCEDURE app.usp_User_GetById
    @UserId             INT,
    @RequestedByUserId  INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId)
                   WHERE IsAdministrator = 1 OR UserId = @UserId)
        THROW 50005, N'FORBIDDEN|You are not allowed to view this user.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE UserId = @UserId)
        THROW 50002, N'USER_NOT_FOUND|The user was not found.', 1;

    SELECT u.UserId, u.FullName, u.Email, r.RoleName, u.IsActive, u.CreatedAtUtc
    FROM dbo.AppUser AS u
    INNER JOIN dbo.AppRole AS r ON r.RoleId = u.RoleId
    WHERE u.UserId = @UserId;
END;
GO
