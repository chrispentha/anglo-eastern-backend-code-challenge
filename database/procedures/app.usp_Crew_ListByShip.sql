/*
    Crew list for one ship as of a date (US-07): crew currently Onboard or Relief Due.
    Signed-off crew (SignOffDate set, even in the future) and planned crew (Sign On in the future) are excluded.

    - Paging        : @PageNumber >= 1, @PageSize 1..100; @TotalCount is correct even for an empty page (D-16)
    - Sorting       : whitelisted keys only, static ORDER BY CASE (no dynamic SQL, SEC-06); 'rank' sorts by
                      seniority (D-15); CrewMemberId is always the final tie-breaker so pages are stable
    - Searching     : one case-insensitive "contains" term OR-ed across every column except Status (D-14),
                      including the 'dd MMM yyyy' date label ('05 Apr'); wildcard characters are escaped so
                      they match literally
    - "Today"       : UTC date unless @AsOfDate is supplied (tests pin it, D-02)
    - Access        : administrator or assigned user; missing and not-assigned both raise 50002 (D-04);
                      a non-operational (inactive) ship raises 50004 SHIP_INACTIVE
*/
CREATE OR ALTER PROCEDURE app.usp_Crew_ListByShip
    @ShipCode           VARCHAR(20),
    @RequestedByUserId  INT,
    @PageNumber         INT = 1,
    @PageSize           INT = 20,
    @SortBy             VARCHAR(30) = 'rank',
    @SortDirection      VARCHAR(10) = 'asc',
    @Search             NVARCHAR(200) = NULL,
    @AsOfDate           DATE = NULL,
    @TotalCount         INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @AsOfDate      = COALESCE(@AsOfDate, CAST(SYSUTCDATETIME() AS DATE));
    SET @ShipCode      = UPPER(LTRIM(RTRIM(@ShipCode)));
    SET @SortBy        = COALESCE(NULLIF(LTRIM(RTRIM(@SortBy)), ''), 'rank');
    SET @SortDirection = LOWER(COALESCE(NULLIF(LTRIM(RTRIM(@SortDirection)), ''), 'asc'));
    SET @Search        = NULLIF(LTRIM(RTRIM(@Search)), N'');

    /* ---- Validate every parameter (the API validates too; never trust the caller) ---- */
    IF @ShipCode IS NULL OR LEN(@ShipCode) NOT BETWEEN 3 AND 10
       OR @ShipCode COLLATE Latin1_General_BIN2 LIKE '%[^A-Z0-9]%'
        THROW 50001, N'VALIDATION_FAILED|shipCode must be 3 to 10 letters or digits.', 1;
    IF @PageNumber IS NULL OR @PageNumber < 1
        THROW 50001, N'VALIDATION_FAILED|pageNumber must be 1 or greater.', 1;
    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100
        THROW 50001, N'VALIDATION_FAILED|pageSize must be between 1 and 100.', 1;
    IF @SortBy NOT IN ('rank', 'crewMemberId', 'firstName', 'lastName', 'age', 'nationality', 'signOnDate', 'status')
        THROW 50001, N'VALIDATION_FAILED|sortBy must be one of: rank, crewMemberId, firstName, lastName, age, nationality, signOnDate, status.', 1;
    IF @SortDirection NOT IN ('asc', 'desc')
        THROW 50001, N'VALIDATION_FAILED|sortDirection must be ''asc'' or ''desc''.', 1;
    IF LEN(@Search) > 100
        THROW 50001, N'VALIDATION_FAILED|search must be at most 100 characters.', 1;

    /* ---- Access and ship status ---- */
    DECLARE @ShipId INT, @IsOperational BIT;

    SELECT @ShipId = ShipId, @IsOperational = IsOperational
    FROM dbo.tvf_ShipForUser(@RequestedByUserId, @ShipCode);

    IF @ShipId IS NULL
    BEGIN
        DECLARE @NotFound NVARCHAR(2048) = CONCAT(N'SHIP_NOT_FOUND|Ship ''', @ShipCode, N''' was not found.');
        THROW 50002, @NotFound, 1;
    END;
    IF @IsOperational = 0
    BEGIN
        DECLARE @Inactive NVARCHAR(2048) = CONCAT(N'SHIP_INACTIVE|Ship ''', @ShipCode, N''' is inactive; crew lists are only available for active ships.');
        THROW 50004, @Inactive, 1;
    END;

    /* ---- Search pattern: user wildcards are escaped so they match literally (SEC-06) ---- */
    DECLARE @Pattern NVARCHAR(400) =
        CASE WHEN @Search IS NULL THEN NULL
             ELSE N'%' + REPLACE(REPLACE(REPLACE(@Search, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%'
        END;

    /* ---- Population: open contracts that have started (uses the filtered index) ---- */
    CREATE TABLE #Crew
    (
        RankName         NVARCHAR(50)   NOT NULL,
        SeniorityOrder   SMALLINT       NOT NULL,
        CrewMemberId     VARCHAR(20)    NOT NULL,
        FirstName        NVARCHAR(100)  NOT NULL,
        LastName         NVARCHAR(100)  NOT NULL,
        Age              INT            NOT NULL,
        Nationality      NVARCHAR(50)   NOT NULL,
        SignOnDate       DATE           NOT NULL,
        SignOnDateLabel  VARCHAR(11)    NOT NULL,
        CrewStatus       VARCHAR(10)    NOT NULL
    );

    INSERT INTO #Crew (RankName, SeniorityOrder, CrewMemberId, FirstName, LastName, Age, Nationality,
                       SignOnDate, SignOnDateLabel, CrewStatus)
    SELECT r.RankName, r.SeniorityOrder, cm.CrewMemberId, cm.FirstName, cm.LastName, ag.Age, c.NationalityName,
           h.SignOnDate, dl.DateLabel, st.CrewStatus
    FROM dbo.CrewServiceHistory AS h
    INNER JOIN dbo.CrewMember AS cm ON cm.CrewMemberId = h.CrewMemberId
    INNER JOIN dbo.CrewRank   AS r  ON r.RankId = h.RankId
    INNER JOIN dbo.Country    AS c  ON c.CountryCode = cm.NationalityCode
    CROSS APPLY dbo.tvf_CrewStatus(h.SignOnDate, h.SignOffDate, h.EndOfContractDate, @AsOfDate) AS st
    CROSS APPLY dbo.tvf_AgeInYears(cm.BirthDate, @AsOfDate) AS ag
    CROSS APPLY dbo.tvf_DateLabel(h.SignOnDate) AS dl
    WHERE h.ShipId = @ShipId
      AND h.SignOffDate IS NULL
      AND h.SignOnDate <= @AsOfDate
      AND st.CrewStatus IN ('Onboard', 'Relief Due')
      AND (@Pattern IS NULL
           OR r.RankName                    LIKE @Pattern
           OR cm.CrewMemberId               LIKE @Pattern
           OR cm.FirstName                  LIKE @Pattern
           OR cm.LastName                   LIKE @Pattern
           OR CAST(ag.Age AS NVARCHAR(10))  LIKE @Pattern
           OR c.NationalityName             LIKE @Pattern
           OR dl.DateLabel                  LIKE @Pattern)   -- Status is intentionally not searchable
    OPTION (RECOMPILE);

    SELECT @TotalCount = COUNT(*) FROM #Crew;

    /* ---- Sort and page: one CASE per key and direction keeps each expression a single data type ---- */
    SELECT RankName, CrewMemberId, FirstName, LastName, Age, Nationality, SignOnDate, SignOnDateLabel,
           Status = CrewStatus
    FROM #Crew
    ORDER BY
        CASE WHEN @SortBy = 'rank'         AND @SortDirection = 'asc'  THEN SeniorityOrder END ASC,
        CASE WHEN @SortBy = 'rank'         AND @SortDirection = 'desc' THEN SeniorityOrder END DESC,
        CASE WHEN @SortBy = 'crewMemberId' AND @SortDirection = 'asc'  THEN CrewMemberId   END ASC,
        CASE WHEN @SortBy = 'crewMemberId' AND @SortDirection = 'desc' THEN CrewMemberId   END DESC,
        CASE WHEN @SortBy = 'firstName'    AND @SortDirection = 'asc'  THEN FirstName      END ASC,
        CASE WHEN @SortBy = 'firstName'    AND @SortDirection = 'desc' THEN FirstName      END DESC,
        CASE WHEN @SortBy = 'lastName'     AND @SortDirection = 'asc'  THEN LastName       END ASC,
        CASE WHEN @SortBy = 'lastName'     AND @SortDirection = 'desc' THEN LastName       END DESC,
        CASE WHEN @SortBy = 'age'          AND @SortDirection = 'asc'  THEN Age            END ASC,
        CASE WHEN @SortBy = 'age'          AND @SortDirection = 'desc' THEN Age            END DESC,
        CASE WHEN @SortBy = 'nationality'  AND @SortDirection = 'asc'  THEN Nationality    END ASC,
        CASE WHEN @SortBy = 'nationality'  AND @SortDirection = 'desc' THEN Nationality    END DESC,
        CASE WHEN @SortBy = 'signOnDate'   AND @SortDirection = 'asc'  THEN SignOnDate     END ASC,
        CASE WHEN @SortBy = 'signOnDate'   AND @SortDirection = 'desc' THEN SignOnDate     END DESC,
        CASE WHEN @SortBy = 'status'       AND @SortDirection = 'asc'  THEN CrewStatus     END ASC,
        CASE WHEN @SortBy = 'status'       AND @SortDirection = 'desc' THEN CrewStatus     END DESC,
        SeniorityOrder ASC,   -- natural secondary order within a sort key
        CrewMemberId ASC      -- unique tie-breaker: deterministic, stable pages
    OFFSET (CAST(@PageNumber AS BIGINT) - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END;
GO
