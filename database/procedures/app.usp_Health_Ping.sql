/* Readiness probe: proves the API can reach the database with its least-privilege login. */
CREATE OR ALTER PROCEDURE app.usp_Health_Ping
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IsHealthy = CAST(1 AS BIT);
END;
GO
