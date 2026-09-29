/*
    Paged list of users (US-02), sorted by name, optionally filtered by role. Administrator only.
    @TotalCount is returned separately so it is correct even when the requested page is empty (D-16).
*/
CREATE OR ALTER PROCEDURE app.usp_User_List
    @RequestedByUserId  INT,
    @PageNumber         INT = 1,
    @PageSize           INT = 20,
    @RoleName           VARCHAR(50) = NULL,
    @SortDirection      VARCHAR(10) = 'asc',
    @TotalCount         INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_UserContext(@RequestedByUserId) WHERE IsAdministrator = 1)
        THROW 50005, N'FORBIDDEN|This operation requires the Administrator role.', 1;

    SET @RoleName = NULLIF(LTRIM(RTRIM(@RoleName)), '');
    SET @SortDirection = LOWER(COALESCE(NULLIF(LTRIM(RTRIM(@SortDirection)), ''), 'asc'));

    IF @PageNumber IS NULL OR @PageNumber < 1
        THROW 50001, N'VALIDATION_FAILED|pageNumber must be 1 or greater.', 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100
        THROW 50001, N'VALIDATION_FAILED|pageSize must be between 1 and 100.', 1;
    IF @SortDirection NOT IN ('asc', 'desc')
        THROW 50001, N'VALIDATION_FAILED|sortDirection must be ''asc'' or ''desc''.', 1;

    DECLARE @RoleId TINYINT = NULL;
    IF @RoleName IS NOT NULL
    BEGIN
        SET @RoleId = (SELECT RoleId FROM dbo.AppRole WHERE RoleName = @RoleName);
        IF @RoleId IS NULL
            THROW 50001, N'VALIDATION_FAILED|role must be an existing role.', 1;
    END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.AppUser AS u
    WHERE @RoleId IS NULL OR u.RoleId = @RoleId;

    SELECT u.UserId, u.FullName, u.Email, r.RoleName, u.IsActive, u.CreatedAtUtc
    FROM dbo.AppUser AS u
    INNER JOIN dbo.AppRole AS r ON r.RoleId = u.RoleId
    WHERE @RoleId IS NULL OR u.RoleId = @RoleId
    ORDER BY
        CASE WHEN @SortDirection = 'asc'  THEN u.FullName END ASC,
        CASE WHEN @SortDirection = 'desc' THEN u.FullName END DESC,
        u.UserId ASC
    OFFSET (CAST(@PageNumber AS BIGINT) - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO
