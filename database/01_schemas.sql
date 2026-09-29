/*
    01_schemas.sql
    Tables, functions and internal procedures live in [dbo].
    Every procedure the API may call lives in [app]; the API's database user is granted
    EXECUTE on schema [app] only (SEC-05). Both schemas are owned by dbo, so ownership
    chaining lets [app] procedures read and write [dbo] tables without table permissions.
*/
IF SCHEMA_ID(N'app') IS NULL
BEGIN
    EXEC (N'CREATE SCHEMA app AUTHORIZATION dbo;');
END;
GO
