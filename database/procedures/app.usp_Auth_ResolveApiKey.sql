/*
    Resolves an API key hash to the caller's security context (SEC-03).
    Result set 1: UserId, RoleName, IsAdministrator  (empty = unknown, revoked or expired key, or inactive user)
    Result set 2: ShipCode of every ship assigned to the user (used for the API-side access check, D-25)
    The raw key never reaches the database; only its SHA-256 hash.
*/
CREATE OR ALTER PROCEDURE app.usp_Auth_ResolveApiKey
    @KeyHash BINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId INT, @ApiKeyId INT, @LastUsedAtUtc DATETIME2(3);
    DECLARE @NowUtc DATETIME2(3) = SYSUTCDATETIME();

    SELECT @UserId = k.UserId, @ApiKeyId = k.ApiKeyId, @LastUsedAtUtc = k.LastUsedAtUtc
    FROM dbo.ApiKey AS k
    WHERE k.KeyHash = @KeyHash
      AND k.RevokedAtUtc IS NULL
      AND (k.ExpiresAtUtc IS NULL OR k.ExpiresAtUtc > @NowUtc);

    SELECT uc.UserId, uc.RoleName, uc.IsAdministrator
    FROM dbo.tvf_UserContext(@UserId) AS uc;

    SELECT s.ShipCode
    FROM dbo.UserShip AS us
    INNER JOIN dbo.Ship AS s ON s.ShipId = us.ShipId
    INNER JOIN dbo.tvf_UserContext(@UserId) AS uc ON uc.UserId = us.UserId
    ORDER BY s.ShipCode;

    -- Usage tracking, throttled to one write per key per 5 minutes to keep authentication read-mostly.
    IF @ApiKeyId IS NOT NULL AND (@LastUsedAtUtc IS NULL OR @LastUsedAtUtc < DATEADD(MINUTE, -5, @NowUtc))
    BEGIN
        UPDATE dbo.ApiKey SET LastUsedAtUtc = @NowUtc WHERE ApiKeyId = @ApiKeyId;
    END;
END;
GO
