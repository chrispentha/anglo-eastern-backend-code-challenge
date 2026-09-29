/*
    Paged ship list (US-04), ordered by ship code, optionally filtered by status.
    Administrators see every ship; other users only the ships assigned to them (SEC-04).
*/
CREATE OR ALTER PROCEDURE app.usp_Ship_List
    @RequestedByUserId  INT,
    @PageNumber         INT = 1,
    @PageSize           INT = 20,
    @StatusName         VARCHAR(30) = NULL,
    @TotalCount         INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IsAdministrator BIT = (SELECT IsAdministrator FROM dbo.tvf_UserContext(@RequestedByUserId));
    IF @IsAdministrator IS NULL
        THROW 50005, N'FORBIDDEN|The caller is not an active user.', 1;

    SET @StatusName = NULLIF(LTRIM(RTRIM(@StatusName)), '');

    IF @PageNumber IS NULL OR @PageNumber < 1
        THROW 50001, N'VALIDATION_FAILED|pageNumber must be 1 or greater.', 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100
        THROW 50001, N'VALIDATION_FAILED|pageSize must be between 1 and 100.', 1;

    DECLARE @ShipStatusId TINYINT = NULL;
    IF @StatusName IS NOT NULL
    BEGIN
        SET @ShipStatusId = (SELECT ShipStatusId FROM dbo.ShipStatus WHERE StatusName = @StatusName);
        IF @ShipStatusId IS NULL
            THROW 50001, N'VALIDATION_FAILED|status must be an existing ship status.', 1;
    END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.Ship AS s
    WHERE (@ShipStatusId IS NULL OR s.ShipStatusId = @ShipStatusId)
      AND (@IsAdministrator = 1
           OR EXISTS (SELECT 1 FROM dbo.UserShip AS us WHERE us.UserId = @RequestedByUserId AND us.ShipId = s.ShipId));

    SELECT s.ShipCode, s.ShipName, s.FiscalYearCode, Status = st.StatusName, s.CreatedAtUtc, s.UpdatedAtUtc, RowVersion = s.RowVer
    FROM dbo.Ship AS s
    INNER JOIN dbo.ShipStatus AS st ON st.ShipStatusId = s.ShipStatusId
    WHERE (@ShipStatusId IS NULL OR s.ShipStatusId = @ShipStatusId)
      AND (@IsAdministrator = 1
           OR EXISTS (SELECT 1 FROM dbo.UserShip AS us WHERE us.UserId = @RequestedByUserId AND us.ShipId = s.ShipId))
    ORDER BY s.ShipCode
    OFFSET (CAST(@PageNumber AS BIGINT) - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO
