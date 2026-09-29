/*
    Summary financial expense report (US-09): parent (summary) accounts only, plus the grand total.
    Built by the same code path as the Detail report, so every summary row ties to the detail row
    for the same account (D-09).
*/
CREATE OR ALTER PROCEDURE app.usp_FinancialReport_Summary
    @ShipCode           VARCHAR(20),
    @Period             DATE,
    @RequestedByUserId  INT,
    @YtdStart           DATE = NULL OUTPUT,
    @YtdEnd             DATE = NULL OUTPUT,
    @FiscalYearCode     CHAR(4) = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    EXEC dbo.usp_FinancialReport_Build
        @ShipCode = @ShipCode,
        @Period = @Period,
        @RequestedByUserId = @RequestedByUserId,
        @ParentsOnly = 1,
        @YtdStart = @YtdStart OUTPUT,
        @YtdEnd = @YtdEnd OUTPUT,
        @FiscalYearCode = @FiscalYearCode OUTPUT;
END;
GO
