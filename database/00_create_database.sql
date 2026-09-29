/*
    00_create_database.sql
    Runs against [master]. Creates the application database if it does not exist.
    Re-runnable. $(DB_NAME) is a sqlcmd variable supplied by deploy.sh.

    - Case-insensitive, accent-sensitive collation: crew search is case-insensitive by default (D-14).
    - READ_COMMITTED_SNAPSHOT: report readers never block writers and vice versa
      (already ON by default in Azure SQL Database, so the ALTER is skipped there).
*/
IF DB_ID(N'$(DB_NAME)') IS NULL
BEGIN
    CREATE DATABASE [$(DB_NAME)] COLLATE Latin1_General_100_CI_AS;
END;
GO

IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'$(DB_NAME)' AND is_read_committed_snapshot_on = 0)
BEGIN
    ALTER DATABASE [$(DB_NAME)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
END;
GO
