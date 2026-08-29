CREATE TABLE [dbo].[Bundles]
(
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(255) NOT NULL,
    [Code] NVARCHAR(100) NULL,
    [Version] NVARCHAR(100) NULL,
    [Status] NVARCHAR(50) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_Bundles_IsActive] DEFAULT(1),
    [CreatedBy] NVARCHAR(255) NULL,
    [CreatedAt] DATETIMEOFFSET(7) NULL CONSTRAINT [DF_Bundles_CreatedAt] DEFAULT(SYSDATETIMEOFFSET()),
    [ModifiedBy] NVARCHAR(255) NULL,
    [ModifiedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_Bundles] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_Bundles_Code]
    ON [dbo].[Bundles] ([Code] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Bundles_Status]
    ON [dbo].[Bundles] ([Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Bundles_IsActive]
    ON [dbo].[Bundles] ([IsActive] ASC);
GO
