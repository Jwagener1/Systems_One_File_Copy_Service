-- Systems One File Copy Service — Database Setup Script
-- Run once against the SQL Server instance before starting the service.
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

-- ── 3. Create sequence for LegacyId ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'LegacyIdSeq' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE SEQUENCE [dbo].[LegacyIdSeq]
        AS INT
        START WITH 1
        INCREMENT BY 1;
    PRINT 'Sequence LegacyIdSeq created.';
END
ELSE
BEGIN
    PRINT 'Sequence LegacyIdSeq already exists — skipped.';
END
GO

-- ── 4. Create ItemLog table ───────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[ItemLog]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[ItemLog]
    (
        [Id]               INT             IDENTITY(1,1)   NOT NULL,
        [ItemDateTime]     DATETIME                        NOT NULL,
        [Barcode]          NVARCHAR(100)                   NULL,
        [Length]           DECIMAL(10,1)                   NULL,
        [Width]            DECIMAL(10,1)                   NULL,
        [Height]           DECIMAL(10,1)                   NULL,
        [Weight]           DECIMAL(18,3)                   NULL,
        [BoxVolume]        BIGINT                          NULL,
        [LiquidVolume]     BIGINT                          NULL,
        [NoDimension]      BIT                             NULL,
        [NoWeight]         BIT                             NULL,
        [Sent]             BIT                             NULL,
        [ImageSent]        BIT                             NULL,
        [Valid]            BIT                             NULL,
        [Complete]         BIT                             NULL,
        [ItemSpec]         SMALLINT                        NULL,
        [ItemCount]        SMALLINT                        NULL,
        [LegacyId]         INT                             NULL
            CONSTRAINT [DF_ItemLog_LegacyId] DEFAULT (NEXT VALUE FOR [dbo].[LegacyIdSeq]),
        [StoreId]          NVARCHAR(32)                    NULL,
        [StoreName]        NVARCHAR(200)                   NULL,
        [NoData]           BIT             NOT NULL
            CONSTRAINT [DF_ItemLog_NoData] DEFAULT ((0)),
        [ErrorDescription] NVARCHAR(500)                   NULL,
        [Direction]        VARCHAR(10)     NOT NULL
            CONSTRAINT [DF_ItemLog_Direction] DEFAULT ('Forward'),
        [TransactionType]  NVARCHAR(20)    NULL
            CONSTRAINT [DF_ItemLog_TransactionType] DEFAULT (N'Normal'),

        CONSTRAINT [PK_ItemLog] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [CK_ItemLog_Direction]
            CHECK ([Direction] = 'Both' OR [Direction] = 'Backward' OR [Direction] = 'Forward')
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
