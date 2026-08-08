IF DB_ID('$(DatabaseName)') IS NULL
BEGIN
    DECLARE @createDb nvarchar(200) = N'CREATE DATABASE [' + REPLACE(N'$(DatabaseName)', N']', N']]') + N']';
    EXEC(@createDb);
END
GO

USE [$(DatabaseName)];
GO

IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees
    (
        Id INT NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Age INT NOT NULL,
        City NVARCHAR(100) NOT NULL,
        Department NVARCHAR(100) NOT NULL,
        IsActive BIT NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        Notes NVARCHAR(200) NULL
    );
END
GO

-- Keep aligned with testdata/Employees.json. Applied by docker compose init only.
IF NOT EXISTS (SELECT 1 FROM dbo.Employees)
BEGIN
    INSERT INTO dbo.Employees (Id, Name, Age, City, Department, IsActive, CreatedAt, Notes)
    VALUES
        (1, N'Asha', 19, N'Delhi', N'Engineering', 1, '2025-01-10T10:00:00Z', N'junior'),
        (2, N'Arun', 24, N'Bengaluru', N'Engineering', 1, '2025-01-11T10:00:00Z', NULL),
        (3, N'Riya', 31, N'Delhi', N'Sales', 1, '2025-01-12T10:00:00Z', N'lead'),
        (4, N'Karan', 22, N'Pune', N'Engineering', 0, '2025-01-13T10:00:00Z', NULL);
END
GO
