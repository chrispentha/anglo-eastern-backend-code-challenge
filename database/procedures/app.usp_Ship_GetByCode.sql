/*
    Returns one ship the caller may access (administrators: any ship; others: assigned ships).
    Missing and not-assigned are indistinguishable: both raise 50002 (D-04, SEC-04).
*/
CREATE OR ALTER PROCEDURE app.usp_Ship_GetByCode
    @ShipCode           VARCHAR(20),
    @RequestedByUserId  INT
AS
BEGIN
    SET NOCOUNT ON;

    SET @ShipCode = UPPER(LTRIM(RTRIM(@ShipCode)));

    IF @ShipCode IS NULL OR LEN(@ShipCode) NOT BETWEEN 3 AND 10
       OR @ShipCode COLLATE Latin1_General_BIN2 LIKE '%[^A-Z0-9]%'
        THROW 50001, N'VALIDATION_FAILED|shipCode must be 3 to 10 letters or digits.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.tvf_ShipForUser(@RequestedByUserId, @ShipCode))
    BEGIN
        DECLARE @NotFound NVARCHAR(2048) = CONCAT(N'SHIP_NOT_FOUND|Ship ''', @ShipCode, N''' was not found.');
        THROW 50002, @NotFound, 1;
    END;

    SELECT s.ShipCode, s.ShipName, s.FiscalYearCode, Status = st.StatusName, s.CreatedAtUtc, s.UpdatedAtUtc, RowVersion = s.RowVer
    FROM dbo.Ship AS s
    INNER JOIN dbo.ShipStatus AS st ON st.ShipStatusId = s.ShipStatusId
    WHERE s.ShipCode = @ShipCode;
END;
GO
