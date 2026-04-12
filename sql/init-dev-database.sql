-- DARAH ECM — Database Initialization Script
-- Run this script in SSMS or sqlcmd after SQL Server is installed
-- Version: 1.0 | April 2026

USE master;
GO

-- Create database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DarahECM_Dev')
BEGIN
    CREATE DATABASE DarahECM_Dev
    COLLATE Arabic_CI_AI;  -- Arabic-aware case-insensitive collation
    PRINT 'Database DarahECM_Dev created successfully.';
END
ELSE
BEGIN
    PRINT 'Database DarahECM_Dev already exists.';
END
GO

USE DarahECM_Dev;
GO

-- ── USERS & ROLES ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        UserId          INT IDENTITY(1,1) PRIMARY KEY,
        Username        NVARCHAR(100) NOT NULL UNIQUE,
        Email           NVARCHAR(255) NOT NULL UNIQUE,
        PasswordHash    NVARCHAR(500) NOT NULL,
        FullNameAr      NVARCHAR(200) NOT NULL,
        FullNameEn      NVARCHAR(200),
        Language        NVARCHAR(5) NOT NULL DEFAULT 'ar',
        IsActive        BIT NOT NULL DEFAULT 1,
        IsLocked        BIT NOT NULL DEFAULT 0,
        LastLoginAt     DATETIME2,
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt       DATETIME2,
        RowVersion      ROWVERSION NOT NULL
    );
    PRINT 'Table Users created.';
END
GO

IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'Roles')
BEGIN
    CREATE TABLE Roles (
        RoleId      INT IDENTITY(1,1) PRIMARY KEY,
        RoleCode    NVARCHAR(50) NOT NULL UNIQUE,
        NameAr      NVARCHAR(200) NOT NULL,
        NameEn      NVARCHAR(200),
        IsActive    BIT NOT NULL DEFAULT 1,
        IsSystem    BIT NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Table Roles created.';
END
GO

IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'UserRoles')
BEGIN
    CREATE TABLE UserRoles (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        UserId      INT NOT NULL REFERENCES Users(UserId),
        RoleId      INT NOT NULL REFERENCES Roles(RoleId),
        IsActive    BIT NOT NULL DEFAULT 1,
        AssignedAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        AssignedBy  INT,
        UNIQUE(UserId, RoleId)
    );
    PRINT 'Table UserRoles created.';
END
GO

IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'Permissions')
BEGIN
    CREATE TABLE Permissions (
        PermissionId    INT IDENTITY(1,1) PRIMARY KEY,
        PermissionCode  NVARCHAR(100) NOT NULL UNIQUE,
        NameAr          NVARCHAR(200) NOT NULL,
        Module          NVARCHAR(50) NOT NULL
    );
    PRINT 'Table Permissions created.';
END
GO

IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'RolePermissions')
BEGIN
    CREATE TABLE RolePermissions (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        RoleId          INT NOT NULL REFERENCES Roles(RoleId),
        PermissionCode  NVARCHAR(100) NOT NULL,
        UNIQUE(RoleId, PermissionCode)
    );
    PRINT 'Table RolePermissions created.';
END
GO

-- ── DOCUMENT LIBRARIES & TYPES ────────────────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'DocumentTypes')
BEGIN
    CREATE TABLE DocumentTypes (
        TypeId          INT IDENTITY(1,1) PRIMARY KEY,
        TypeCode        NVARCHAR(50) NOT NULL UNIQUE,
        NameAr          NVARCHAR(200) NOT NULL,
        NameEn          NVARCHAR(200),
        IsActive        BIT NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Table DocumentTypes created.';
END
GO

IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'DocumentLibraries')
BEGIN
    CREATE TABLE DocumentLibraries (
        LibraryId       INT IDENTITY(1,1) PRIMARY KEY,
        LibraryCode     NVARCHAR(50) NOT NULL UNIQUE,
        NameAr          NVARCHAR(300) NOT NULL,
        NameEn          NVARCHAR(300),
        IsActive        BIT NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy       INT REFERENCES Users(UserId)
    );
    PRINT 'Table DocumentLibraries created.';
END
GO

-- ── DOCUMENTS ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'Documents')
BEGIN
    CREATE TABLE Documents (
        DocumentId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        DocumentNumber          NVARCHAR(50) NOT NULL UNIQUE,
        TitleAr                 NVARCHAR(500) NOT NULL,
        TitleEn                 NVARCHAR(500),
        DocumentTypeId          INT NOT NULL REFERENCES DocumentTypes(TypeId),
        LibraryId               INT NOT NULL REFERENCES DocumentLibraries(LibraryId),
        FolderId                INT,
        Status                  NVARCHAR(20) NOT NULL DEFAULT 'DRAFT',
        ClassificationCode      NVARCHAR(20) NOT NULL DEFAULT 'INTERNAL',
        ClassificationOrder     INT NOT NULL DEFAULT 2,
        CurrentVersionId        INT NOT NULL DEFAULT 0,
        IsCheckedOut            BIT NOT NULL DEFAULT 0,
        CheckedOutBy            INT REFERENCES Users(UserId),
        CheckedOutAt            DATETIME2,
        IsLegalHold             BIT NOT NULL DEFAULT 0,
        IsDeleted               BIT NOT NULL DEFAULT 0,
        DeletedAt               DATETIME2,
        DeletedBy               INT,
        DocumentDate            DATE,
        Keywords                NVARCHAR(1000),
        Summary                 NVARCHAR(2000),
        RetentionPolicyId       INT,
        RetentionExpiresAt      DATE,
        RecordClassId           INT,
        PrimaryWorkspaceId      UNIQUEIDENTIFIER,
        CreatedAt               DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy               INT REFERENCES Users(UserId),
        UpdatedAt               DATETIME2,
        UpdatedBy               INT,
        RowVersion              ROWVERSION NOT NULL
    );

    CREATE INDEX IX_Documents_Status ON Documents(Status) WHERE IsDeleted = 0;
    CREATE INDEX IX_Documents_LibraryId ON Documents(LibraryId) WHERE IsDeleted = 0;
    CREATE INDEX IX_Documents_CreatedBy ON Documents(CreatedBy);
    CREATE FULLTEXT CATALOG ECM_FullText AS DEFAULT;
    PRINT 'Table Documents created with indexes.';
END
GO

-- ── DOCUMENT VERSIONS ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'DocumentVersions')
BEGIN
    CREATE TABLE DocumentVersions (
        VersionId           INT IDENTITY(1,1) PRIMARY KEY,
        DocumentId          UNIQUEIDENTIFIER NOT NULL REFERENCES Documents(DocumentId),
        VersionNumber       NVARCHAR(10) NOT NULL,
        MajorVersion        INT NOT NULL,
        MinorVersion        INT NOT NULL,
        StorageKey          NVARCHAR(500) NOT NULL,
        OriginalFileName    NVARCHAR(260) NOT NULL,
        FileExtension       NVARCHAR(10) NOT NULL,
        FileSizeBytes       BIGINT NOT NULL,
        ContentType         NVARCHAR(200) NOT NULL,
        ContentHash         NVARCHAR(64) NOT NULL,
        StorageProvider     NVARCHAR(50) NOT NULL DEFAULT 'LocalFileSystem',
        IsCurrent           BIT NOT NULL DEFAULT 1,
        ChangeNote          NVARCHAR(500),
        CheckInNote         NVARCHAR(500),
        CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy           INT REFERENCES Users(UserId)
    );

    CREATE INDEX IX_DocumentVersions_DocumentId ON DocumentVersions(DocumentId);
    PRINT 'Table DocumentVersions created.';
END
GO

-- ── AUDIT LOGS ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE AuditLogs (
        AuditId         BIGINT IDENTITY(1,1) PRIMARY KEY,
        CorrelationId   NVARCHAR(50),
        EventType       NVARCHAR(100) NOT NULL,
        Module          NVARCHAR(50),
        EntityType      NVARCHAR(100),
        EntityId        NVARCHAR(200),
        UserId          INT,
        Username        NVARCHAR(100),
        IPAddress       NVARCHAR(50),
        OldValues       NVARCHAR(MAX),
        NewValues       NVARCHAR(MAX),
        AdditionalInfo  NVARCHAR(MAX),
        Severity        NVARCHAR(20) NOT NULL DEFAULT 'Info',
        IsSuccessful    BIT NOT NULL DEFAULT 1,
        FailureReason   NVARCHAR(500),
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_AuditLogs_EventType ON AuditLogs(EventType, CreatedAt DESC);
    CREATE INDEX IX_AuditLogs_EntityId ON AuditLogs(EntityType, EntityId);
    CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId, CreatedAt DESC);
    CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt DESC);
    PRINT 'Table AuditLogs created with indexes.';
END
GO

-- ── NOTIFICATIONS ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.tables WHERE name = 'Notifications')
BEGIN
    CREATE TABLE Notifications (
        NotificationId      BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId              INT NOT NULL REFERENCES Users(UserId),
        Title               NVARCHAR(300) NOT NULL,
        Body                NVARCHAR(1000) NOT NULL,
        NotificationType    NVARCHAR(50) NOT NULL,
        EntityType          NVARCHAR(50),
        EntityId            NVARCHAR(200),
        ActionUrl           NVARCHAR(500),
        Priority            INT NOT NULL DEFAULT 2,
        IsRead              BIT NOT NULL DEFAULT 0,
        ReadAt              DATETIME2,
        ExpiresAt           DATETIME2,
        CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_Notifications_UserId_IsRead ON Notifications(UserId, IsRead, CreatedAt DESC);
    PRINT 'Table Notifications created.';
END
GO

-- ── SEED DATA ─────────────────────────────────────────────────────────────────
-- Default roles
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleCode = 'SYSTEM_ADMIN')
BEGIN
    INSERT INTO Roles (RoleCode, NameAr, NameEn, IsSystem) VALUES
        ('SYSTEM_ADMIN',     'مدير النظام',       'System Administrator', 1),
        ('CONTENT_MANAGER',  'مدير المحتوى',      'Content Manager',      0),
        ('DEPT_HEAD',        'مدير القسم',         'Department Head',      0),
        ('RECORDS_MANAGER',  'مدير السجلات',      'Records Manager',      0),
        ('LEGAL_OFFICER',    'المستشار القانوني', 'Legal Officer',        0),
        ('BASIC_USER',       'مستخدم عادي',       'Basic User',           0);
    PRINT 'Default roles seeded.';
END
GO

-- Default permissions for System Admin role
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = 1 AND PermissionCode = 'admin.system')
BEGIN
    INSERT INTO RolePermissions (RoleId, PermissionCode)
    SELECT 1, p FROM (VALUES
        ('admin.system'), ('documents.create'), ('documents.read'), ('documents.update'),
        ('documents.delete'), ('documents.download'), ('documents.checkout'), ('documents.checkin'),
        ('documents.access.secret'), ('documents.access.confidential'),
        ('workflow.submit'), ('workflow.approve'), ('workflow.delegate'),
        ('admin.retention'), ('workspace.create'), ('workspace.update'), ('workspace.manage'),
        ('audit.read'), ('audit.export')
    ) AS perms(p);
    PRINT 'System Admin permissions seeded.';
END
GO

-- Default admin user (password: Admin@2026!)
-- BCrypt hash of "Admin@2026!" — change immediately in production!
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (Username, Email, PasswordHash, FullNameAr, FullNameEn, Language)
    VALUES (
        'admin',
        'admin@darah.gov.sa',
        '$2a$11$rBV2JDeWW3DWVCwyN0RUGOKepxSbs4e71OOYML6qLYL1z6c5bWBHq',
        'مدير النظام',
        'System Administrator',
        'ar'
    );

    -- Assign System Admin role
    INSERT INTO UserRoles (UserId, RoleId)
    VALUES (SCOPE_IDENTITY(), 1);

    PRINT 'Default admin user created. Username: admin | Password: Admin@2026!';
    PRINT 'IMPORTANT: Change this password immediately!';
END
GO

-- Default document types
IF NOT EXISTS (SELECT 1 FROM DocumentTypes WHERE TypeCode = 'CONTRACT')
BEGIN
    INSERT INTO DocumentTypes (TypeCode, NameAr, NameEn) VALUES
        ('CONTRACT',    'عقد',                   'Contract'),
        ('REPORT',      'تقرير',                  'Report'),
        ('MEMO',        'مذكرة',                  'Memorandum'),
        ('POLICY',      'سياسة',                  'Policy'),
        ('PROCEDURE',   'إجراء',                  'Procedure'),
        ('INVOICE',     'فاتورة',                 'Invoice'),
        ('LETTER',      'خطاب',                   'Letter'),
        ('FORM',        'نموذج',                  'Form'),
        ('MANUAL',      'دليل',                   'Manual'),
        ('GENERAL',     'عام',                    'General');
    PRINT 'Default document types seeded.';
END
GO

-- Default document library
IF NOT EXISTS (SELECT 1 FROM DocumentLibraries WHERE LibraryCode = 'GENERAL')
BEGIN
    INSERT INTO DocumentLibraries (LibraryCode, NameAr, NameEn, CreatedBy) VALUES
        ('GENERAL',    'المكتبة العامة',   'General Library',   1),
        ('CONTRACTS',  'مكتبة العقود',    'Contracts Library', 1),
        ('HR',         'الموارد البشرية', 'HR Library',        1),
        ('FINANCE',    'المالية',          'Finance Library',   1),
        ('LEGAL',      'الشؤون القانونية','Legal Library',     1);
    PRINT 'Default document libraries seeded.';
END
GO

PRINT '=== DarahECM_Dev database initialization complete ===';
PRINT 'Default admin credentials:';
PRINT '  Username: admin';
PRINT '  Password: Admin@2026!';
PRINT 'IMPORTANT: Change the admin password before using the system!';
GO
