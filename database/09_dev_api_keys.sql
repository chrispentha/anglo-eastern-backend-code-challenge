/*
    09_dev_api_keys.sql
    LOCAL DEVELOPMENT API keys for the six sample users. Run by deploy.sh only when SEED_SAMPLE_DATA=1.

    - Keys are random: 32 bytes from CRYPT_GEN_RANDOM (cryptographically secure), Base64Url-encoded, in the
      API's format sm_<prefix>_<secret>. No key is stored in the repository.
    - Only the SHA-256 hash is stored (SEC-03). The raw keys are printed ONCE, in this script's output
      (`docker compose logs db-init`), and cannot be recovered afterwards.
    - Keys are issued when a sample user has no active development key (first deployment, or after expiry or
      revocation), so an ordinary redeploy does not invalidate keys already in use.
      ROTATE_DEV_KEYS=1 revokes the current development keys and issues new ones.
    - Keys expire 180 days after issue.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Rotate BIT = CASE WHEN N'$(ROTATE_DEV_KEYS)' = N'1' THEN 1 ELSE 0 END;
DECLARE @NowUtc DATETIME2(3) = SYSUTCDATETIME();

DECLARE @DevUser TABLE (Ordinal INT PRIMARY KEY, Email NVARCHAR(254), KeyPrefix CHAR(8), UserId INT NULL, RoleName VARCHAR(30) NULL);
INSERT INTO @DevUser (Ordinal, Email, KeyPrefix)
VALUES (1, N'alex.morgan@example.com',   'devadm01'),
       (2, N'priya.nair@example.com',    'devcrw01'),
       (3, N'daniel.chen@example.com',   'devacc01'),
       (4, N'sofia.andreou@example.com', 'devown01'),
       (5, N'marco.rossi@example.com',   'devsup01'),
       (6, N'hannah.lee@example.com',    'devflt01');

UPDATE d
SET d.UserId = u.UserId, d.RoleName = r.RoleName
FROM @DevUser AS d
JOIN dbo.AppUser AS u ON u.Email = d.Email AND u.IsActive = 1
JOIN dbo.AppRole AS r ON r.RoleId = u.RoleId;

DELETE FROM @DevUser WHERE UserId IS NULL;   -- sample user removed or deactivated: no key

IF NOT EXISTS (SELECT 1 FROM @DevUser)
BEGIN
    PRINT 'No sample users found - no development API keys issued.';
    RETURN;
END;

IF @Rotate = 0
   AND NOT EXISTS (
        SELECT 1 FROM @DevUser AS d
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ApiKey AS k
                          WHERE k.UserId = d.UserId AND k.KeyPrefix = d.KeyPrefix
                            AND k.RevokedAtUtc IS NULL AND (k.ExpiresAtUtc IS NULL OR k.ExpiresAtUtc > @NowUtc)))
BEGIN
    PRINT 'Development API keys are already issued (printed when they were created).';
    PRINT 'To issue new ones:  docker compose run --rm -e ROTATE_DEV_KEYS=1 db-init';
    RETURN;
END;

DECLARE @Issued TABLE (Ordinal INT, RoleName VARCHAR(30), Email NVARCHAR(254), RawKey VARCHAR(60));

BEGIN TRANSACTION;

-- Retire the previous development keys of these users (idempotent; history is kept, D-20).
UPDATE k
SET k.RevokedAtUtc = @NowUtc
FROM dbo.ApiKey AS k
JOIN @DevUser AS d ON d.UserId = k.UserId AND d.KeyPrefix = k.KeyPrefix
WHERE k.RevokedAtUtc IS NULL;

DECLARE @Ordinal INT = (SELECT MIN(Ordinal) FROM @DevUser);
WHILE @Ordinal IS NOT NULL
BEGIN
    DECLARE @Random VARBINARY(32) = CRYPT_GEN_RANDOM(32);
    DECLARE @Base64 VARCHAR(64) = CAST(N'' AS XML).value('xs:base64Binary(sql:variable("@Random"))', 'VARCHAR(64)');
    DECLARE @Secret VARCHAR(43) = REPLACE(REPLACE(REPLACE(@Base64, '+', '-'), '/', '_'), '=', '');
    DECLARE @RawKey VARCHAR(60) = (SELECT CONCAT('sm_', KeyPrefix, '_', @Secret) FROM @DevUser WHERE Ordinal = @Ordinal);

    INSERT INTO dbo.ApiKey (UserId, KeyPrefix, KeyHash, ExpiresAtUtc)
    SELECT d.UserId, d.KeyPrefix, HASHBYTES('SHA2_256', @RawKey), DATEADD(DAY, 180, @NowUtc)
    FROM @DevUser AS d
    WHERE d.Ordinal = @Ordinal;

    INSERT INTO @Issued (Ordinal, RoleName, Email, RawKey)
    SELECT Ordinal, RoleName, Email, @RawKey FROM @DevUser WHERE Ordinal = @Ordinal;

    SET @Ordinal = (SELECT MIN(Ordinal) FROM @DevUser WHERE Ordinal > @Ordinal);
END;

INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
VALUES (NULL, 'DEV_API_KEYS_ISSUED', 'ApiKey', N'09_dev_api_keys.sql',
        CONCAT(N'count=', (SELECT COUNT(*) FROM @Issued), N'; rotate=', @Rotate));

COMMIT TRANSACTION;

-- The only time the raw keys are ever visible. Copy them now.
PRINT '';
PRINT '================ LOCAL DEVELOPMENT API KEYS (shown once, expire in 180 days) ================';
DECLARE @Line NVARCHAR(400);
DECLARE @Next INT = (SELECT MIN(Ordinal) FROM @Issued);
WHILE @Next IS NOT NULL
BEGIN
    SELECT @Line = CONCAT(LEFT(RoleName + SPACE(20), 20), LEFT(Email + SPACE(28), 28), RawKey) FROM @Issued WHERE Ordinal = @Next;
    PRINT @Line;
    SET @Next = (SELECT MIN(Ordinal) FROM @Issued WHERE Ordinal > @Next);
END;
PRINT '==============================================================================================';
GO
