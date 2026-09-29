/*
    Detail financial expense report (US-08): every qualifying account, parents and children, in tree order.
    Output parameters give the fiscal YTD window so the API can label columns ("Actual YTD (Apr 2024 - Feb 2025)").
    Errors: 50001 validation, 50002 ship not found / not accessible, 50004 ship inactive.
*/
CREATE OR ALTER PROCEDURE app.usp_FinancialReport_Detail
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
        @ParentsOnly = 0,
        @YtdStart = @YtdStart OUTPUT,
        @YtdEnd = @YtdEnd OUTPUT,
        @FiscalYearCode = @FiscalYearCode OUTPUT;
END;
GO
