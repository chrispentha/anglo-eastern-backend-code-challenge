/*
    06_security.sql
    Least-privilege database principal for the API (SEC-05).

    - Role [app_executor] has EXECUTE on schema [app] and nothing else: no SELECT / INSERT / UPDATE / DELETE
      on any table, no db_datareader / db_datawriter / db_owner. Procedures reach the tables through
      ownership chaining (everything is owned by dbo), which is also why dynamic SQL is not used anywhere:
      it would break the chain and require table grants.
    - The API login/user is a member of that role only.
    - Credentials come from sqlcmd variables supplied by deploy.sh from environment variables (SEC-02).
      The password must not contain a single quote.

    SQL Server / Express: server login + database user.
    Azure SQL Database (EngineEdition 5): contained database user (no server login), or better, a
    Microsoft Entra managed identity: CREATE USER [<identity-name>] FROM EXTERNAL PROVIDER.
*/
IF DATABASE_PRINCIPAL_ID(N'app_executor') IS NULL
BEGIN
    CREATE ROLE app_executor AUTHORIZATION dbo;
END;
GO

GRANT EXECUTE ON SCHEMA::app TO app_executor;
GO

IF CAST(SERVERPROPERTY('EngineEdition') AS INT) = 5
BEGIN
    -- Azure SQL Database: contained user with password.
    IF DATABASE_PRINCIPAL_ID(N'$(APP_DB_USER)') IS NULL
        EXEC (N'CREATE USER [$(APP_DB_USER)] WITH PASSWORD = N''$(APP_DB_PASSWORD)'';');
    ELSE
        EXEC (N'ALTER USER [$(APP_DB_USER)] WITH PASSWORD = N''$(APP_DB_PASSWORD)'';');
END
ELSE
BEGIN
    -- SQL Server / Express: server login (password policy enforced) mapped to a database user.
    IF SUSER_ID(N'$(APP_DB_USER)') IS NULL
        EXEC (N'CREATE LOGIN [$(APP_DB_USER)] WITH PASSWORD = N''$(APP_DB_PASSWORD)'', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DB_NAME)];');
    ELSE
        EXEC (N'ALTER LOGIN [$(APP_DB_USER)] WITH PASSWORD = N''$(APP_DB_PASSWORD)'';');

    IF DATABASE_PRINCIPAL_ID(N'$(APP_DB_USER)') IS NULL
        EXEC (N'CREATE USER [$(APP_DB_USER)] FOR LOGIN [$(APP_DB_USER)] WITH DEFAULT_SCHEMA = app;');
END;
GO

IF IS_ROLEMEMBER(N'app_executor', N'$(APP_DB_USER)') = 0
BEGIN
    ALTER ROLE app_executor ADD MEMBER [$(APP_DB_USER)];
END;
GO
