/* Key metadata for a user (never the hash). Administrator only. */
CREATE OR ALTER PROCEDURE app.usp_ApiKey_ListByUser
    @UserId             INT,
    @RequestedByUserId  INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE UserId = @UserId)
        THROW 50002, N'USER_NOT_FOUND|The user was not found.', 1;

    SELECT ApiKeyId, UserId, KeyPrefix, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, LastUsedAtUtc
    FROM dbo.ApiKey
    WHERE UserId = @UserId
    ORDER BY ApiKeyId;
END;
GO
