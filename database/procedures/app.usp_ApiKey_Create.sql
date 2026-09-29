/*
    Registers a new API key for a user (D-24). The API generates the key, returns the raw value to the
    administrator once, and passes only its SHA-256 hash and non-secret prefix here. Administrator only.
*/
CREATE OR ALTER PROCEDURE app.usp_ApiKey_Create
    @UserId             INT,
    @KeyPrefix          VARCHAR(20),
    @KeyHash            VARBINARY(64),
    @ExpiresAtUtc       DATETIME2(3) = NULL,
    @RequestedByUserId  INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    IF @KeyPrefix IS NULL OR LEN(@KeyPrefix) <> 8 OR @KeyPrefix COLLATE Latin1_General_BIN2 LIKE '%[^a-z0-9]%'
        THROW 50001, N'VALIDATION_FAILED|keyPrefix must be 8 lower-case letters or digits.', 1;
    IF @KeyHash IS NULL OR DATALENGTH(@KeyHash) <> 32
        THROW 50001, N'VALIDATION_FAILED|keyHash must be a 32-byte SHA-256 hash.', 1;
    IF @ExpiresAtUtc IS NOT NULL AND @ExpiresAtUtc <= SYSUTCDATETIME()
        THROW 50001, N'VALIDATION_FAILED|expiresAtUtc must be in the future.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE UserId = @UserId AND IsActive = 1)
        THROW 50002, N'USER_NOT_FOUND|The user was not found or is inactive.', 1;

    DECLARE @ApiKeyId INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.ApiKey (UserId, KeyPrefix, KeyHash, ExpiresAtUtc, CreatedByUserId)
        VALUES (@UserId, @KeyPrefix, CAST(@KeyHash AS BINARY(32)), @ExpiresAtUtc, @RequestedByUserId);

        SET @ApiKeyId = CAST(SCOPE_IDENTITY() AS INT);

        INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
        VALUES (@RequestedByUserId, 'API_KEY_CREATED', 'ApiKey', CAST(@ApiKeyId AS NVARCHAR(50)),
                N'userId=' + CAST(@UserId AS NVARCHAR(20)) + N'; prefix=' + @KeyPrefix);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT ApiKeyId, UserId, KeyPrefix, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, LastUsedAtUtc
    FROM dbo.ApiKey
    WHERE ApiKeyId = @ApiKeyId;
END;
GO
