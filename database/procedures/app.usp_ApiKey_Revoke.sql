/*
    Revokes an API key (idempotent: revoking a revoked key is not an error). Administrator only.
    Errors: 50002 key not found for that user.
*/
CREATE OR ALTER PROCEDURE app.usp_ApiKey_Revoke
    @UserId             INT,
    @ApiKeyId           INT,
    @RequestedByUserId  INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.ApiKey WHERE ApiKeyId = @ApiKeyId AND UserId = @UserId)
        THROW 50002, N'API_KEY_NOT_FOUND|The API key was not found.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.ApiKey
        SET RevokedAtUtc = SYSUTCDATETIME()
        WHERE ApiKeyId = @ApiKeyId AND RevokedAtUtc IS NULL;

        IF @@ROWCOUNT > 0
            INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
            VALUES (@RequestedByUserId, 'API_KEY_REVOKED', 'ApiKey', CAST(@ApiKeyId AS NVARCHAR(50)),
                    N'userId=' + CAST(@UserId AS NVARCHAR(20)));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
