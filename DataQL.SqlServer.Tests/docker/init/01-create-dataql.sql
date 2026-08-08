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
        Notes NVARCHAR(200) NULL,
        Tags NVARCHAR(MAX) NULL,
        Skills NVARCHAR(MAX) NULL,
        Address NVARCHAR(MAX) NULL,
        Projects NVARCHAR(MAX) NULL
    );
END
GO

-- Upgrade existing volumes created before JSON columns existed.
IF COL_LENGTH(N'dbo.Employees', N'Tags') IS NULL
    ALTER TABLE dbo.Employees ADD Tags NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employees', N'Skills') IS NULL
    ALTER TABLE dbo.Employees ADD Skills NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employees', N'Address') IS NULL
    ALTER TABLE dbo.Employees ADD Address NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Employees', N'Projects') IS NULL
    ALTER TABLE dbo.Employees ADD Projects NVARCHAR(MAX) NULL;
GO

-- Keep aligned with testdata/Employees.json. Applied by docker compose init only.
IF NOT EXISTS (SELECT 1 FROM dbo.Employees)
BEGIN
    INSERT INTO dbo.Employees
        (Id, Name, Age, City, Department, IsActive, CreatedAt, Notes, Tags, Skills, Address, Projects)
    VALUES
        (1, N'Asha', 19, N'Delhi', N'Engineering', 1, '2025-01-10T10:00:00Z', N'junior',
         N'["junior","remote"]', N'["C#",".NET"]',
         N'{"City":"Delhi","Country":"India"}',
         N'[{"Name":"Alpha","Status":"Active","Hours":30}]'),
        (2, N'Arun', 24, N'Bengaluru', N'Engineering', 1, '2025-01-11T10:00:00Z', NULL,
         N'["senior"]', N'["Java","Azure"]',
         N'{"City":"Bengaluru","Country":"India"}',
         N'[{"Name":"Beta","Status":"Done","Hours":10}]'),
        (3, N'Riya', 31, N'Delhi', N'Sales', 1, '2025-01-12T10:00:00Z', N'lead',
         N'["lead","remote","sales"]', N'["Azure",".NET","SQL"]',
         N'{"City":"Delhi","Country":"India"}',
         N'[{"Name":"Gamma","Status":"Active","Hours":25},{"Name":"Delta","Status":"Active","Hours":5}]'),
        (4, N'Karan', 22, N'Pune', N'Engineering', 0, '2025-01-13T10:00:00Z', NULL,
         N'[]', N'[]',
         N'{"City":"Pune","Country":"India"}',
         N'[]');
END
GO

-- Refresh JSON seed columns for upgraded volumes (tests are read-only after init).
UPDATE dbo.Employees
SET
    Tags = CASE Id
        WHEN 1 THEN N'["junior","remote"]'
        WHEN 2 THEN N'["senior"]'
        WHEN 3 THEN N'["lead","remote","sales"]'
        WHEN 4 THEN N'[]'
        ELSE Tags
    END,
    Skills = CASE Id
        WHEN 1 THEN N'["C#",".NET"]'
        WHEN 2 THEN N'["Java","Azure"]'
        WHEN 3 THEN N'["Azure",".NET","SQL"]'
        WHEN 4 THEN N'[]'
        ELSE Skills
    END,
    Address = CASE Id
        WHEN 1 THEN N'{"City":"Delhi","Country":"India"}'
        WHEN 2 THEN N'{"City":"Bengaluru","Country":"India"}'
        WHEN 3 THEN N'{"City":"Delhi","Country":"India"}'
        WHEN 4 THEN N'{"City":"Pune","Country":"India"}'
        ELSE Address
    END,
    Projects = CASE Id
        WHEN 1 THEN N'[{"Name":"Alpha","Status":"Active","Hours":30}]'
        WHEN 2 THEN N'[{"Name":"Beta","Status":"Done","Hours":10}]'
        WHEN 3 THEN N'[{"Name":"Gamma","Status":"Active","Hours":25},{"Name":"Delta","Status":"Active","Hours":5}]'
        WHEN 4 THEN N'[]'
        ELSE Projects
    END
WHERE Id IN (1, 2, 3, 4);
GO
