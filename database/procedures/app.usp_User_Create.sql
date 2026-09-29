/*
    Creates an application user (US-01). Administrator only (SEC-04).
    Errors: 50001 validation, 50003 duplicate email, 50005 caller is not an administrator.
*/
CREATE OR ALTER PROCEDURE app.usp_User_Create
    @FullName           NVARCHAR(200),
    @Email              NVARCHAR(300) = NULL,
    @RoleName           VARCHAR(50),
    @RequestedByUserId  INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    -- Parameters are wider than the columns so over-long input is rejected, never silently truncated.
    SET @FullName = LTRIM(RTRIM(@FullName));
    SET @Email = NULLIF(LTRIM(RTRIM(@Email)), N'');
    SET @RoleName = LTRIM(RTRIM(@RoleName));

    IF @FullName IS NULL OR LEN(@FullName) = 0 OR LEN(@FullName) > 100
        THROW 50001, N'VALIDATION_FAILED|fullName is required and must be at most 100 characters.', 1;
    IF @Email IS NOT NULL AND (LEN(@Email) > 254 OR @Email NOT LIKE N'%_@_%._%')
        THROW 50001, N'VALIDATION_FAILED|email must be a valid e-mail address of at most 254 characters.', 1;

    DECLARE @RoleId TINYINT = (SELECT RoleId FROM dbo.AppRole WHERE RoleName = @RoleName);
    IF @RoleId IS NULL
        THROW 50001, N'VALIDATION_FAILED|role must be an existing role.', 1;

    DECLARE @UserId INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @Email IS NOT NULL
           AND EXISTS (SELECT 1 FROM dbo.AppUser WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email)
            THROW 50003, N'USER_EMAIL_CONFLICT|A user with this email already exists.', 1;

        INSERT INTO dbo.AppUser (FullName, Email, RoleId)
        VALUES (@FullName, @Email, @RoleId);

        SET @UserId = CAST(SCOPE_IDENTITY() AS INT);

        INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
        VALUES (@RequestedByUserId, 'USER_CREATED', 'AppUser', CAST(@UserId AS NVARCHAR(50)), N'role=' + @RoleName);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT u.UserId, u.FullName, u.Email, r.RoleName, u.IsActive, u.CreatedAtUtc
    FROM dbo.AppUser AS u
    INNER JOIN dbo.AppRole AS r ON r.RoleId = u.RoleId
    WHERE u.UserId = @UserId;
END;
GO
