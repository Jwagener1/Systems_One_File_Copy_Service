-- Systems One File Copy Service — Database Setup Script
-- Run this script once against the SQL Server instance before starting the service.
-- Execute as a sysadmin login (e.g. sa).

-- ── 1. Create database ────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'Systems_One')
BEGIN
    CREATE DATABASE [Systems_One];
END
GO

USE [Systems_One];
GO

-- ── 2. Create SQL login and database user ─────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = N'SysOne')
BEGIN
    CREATE LOGIN [SysOne] WITH PASSWORD = N'SysOne012!',
        DEFAULT_DATABASE = [Systems_One],
        CHECK_EXPIRATION = OFF,
        CHECK_POLICY = OFF;
END
GO

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'SysOne')
BEGIN
    CREATE USER [SysOne] FOR LOGIN [SysOne];
END
GO

ALTER ROLE [db_datareader] ADD MEMBER [SysOne];
ALTER ROLE [db_datawriter] ADD MEMBER [SysOne];
GO

-- ── 3. Create ItemLog table ───────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[ItemLog]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[ItemLog]
    (
        [Id]           INT              IDENTITY(1,1)   NOT NULL,
        [Barcode]      NVARCHAR(200)                    NULL,
        [ItemDateTime] DATETIME2(7)                     NOT NULL,
        [Length]       DECIMAL(18, 4)                   NOT NULL  CONSTRAINT [DF_ItemLog_Length]       DEFAULT (0),
        [Width]        DECIMAL(18, 4)                   NOT NULL  CONSTRAINT [DF_ItemLog_Width]        DEFAULT (0),
        [Height]       DECIMAL(18, 4)                   NOT NULL  CONSTRAINT [DF_ItemLog_Height]       DEFAULT (0),
        [Weight]       DECIMAL(18, 4)                   NOT NULL  CONSTRAINT [DF_ItemLog_Weight]       DEFAULT (0),
        [BoxVolume]    DECIMAL(18, 4)                   NOT NULL  CONSTRAINT [DF_ItemLog_BoxVolume]    DEFAULT (0),
        [LiquidVolume] DECIMAL(18, 4)                   NOT NULL  CONSTRAINT [DF_ItemLog_LiquidVolume] DEFAULT (0),
        [ItemCount]    INT                              NOT NULL  CONSTRAINT [DF_ItemLog_ItemCount]    DEFAULT (0),
        [ItemSpec]     NVARCHAR(500)                    NULL,
        [Valid]        BIT                              NOT NULL  CONSTRAINT [DF_ItemLog_Valid]        DEFAULT (0),
        [Sent]         BIT                              NULL,
        [Complete]     BIT                              NULL,

        CONSTRAINT [PK_ItemLog] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_ItemLog_Valid_Sent]
        ON [dbo].[ItemLog] ([Valid] ASC, [Sent] ASC)
        INCLUDE ([ItemDateTime]);

    PRINT 'ItemLog table created.';
END
ELSE
BEGIN
    PRINT 'ItemLog table already exists — skipped.';
END
GO

PRINT 'Setup complete.';
GO
