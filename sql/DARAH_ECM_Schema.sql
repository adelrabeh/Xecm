-- ============================================================
-- DARAH ECM — Enterprise Content Management System
-- SQL Server Database Schema — Version 1.0
-- Database: DARAH_ECM
-- Collation: Arabic_CI_AI (supports Arabic + English)
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DARAH_ECM')
BEGIN
    CREATE DATABASE DARAH_ECM
    COLLATE Arabic_CI_AI;
END
GO

USE DARAH_ECM;
GO

-- Enable Full-Text Search
IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ECM_FTS_Catalog')
    CREATE FULLTEXT CATALOG ECM_FTS_Catalog AS DEFAULT;
GO

-- ============================================================
-- SECTION 1: ADMINISTRATION & MASTER DATA
-- ============================================================

CREATE TABLE Languages (
    LanguageId      INT             NOT NULL IDENTITY(1,1),
    Code            CHAR(2)         NOT NULL,
    NameAr          NVARCHAR(100)   NOT NULL,
    NameEn          NVARCHAR(100)   NOT NULL,
    IsDefault       BIT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    SortOrder       INT             NOT NULL DEFAULT 0,
    CONSTRAINT PK_Languages PRIMARY KEY (LanguageId),
    CONSTRAINT UQ_Languages_Code UNIQUE (Code)
);

CREATE TABLE Organizations (
    OrganizationId  INT             NOT NULL IDENTITY(1,1),
    Code            NVARCHAR(50)    NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    LogoPath        NVARCHAR(500)   NULL,
    WebsiteUrl      NVARCHAR(500)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_Organizations PRIMARY KEY (OrganizationId),
    CONSTRAINT UQ_Organizations_Code UNIQUE (Code)
);

CREATE TABLE Departments (
    DepartmentId    INT             NOT NULL IDENTITY(1,1),
    ParentId        INT             NULL,
    OrganizationId  INT             NOT NULL,
    Code            NVARCHAR(50)    NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    ManagerId       INT             NULL,  -- FK added after Users table
    IsActive        BIT             NOT NULL DEFAULT 1,
    SortOrder       INT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_Departments PRIMARY KEY (DepartmentId),
    CONSTRAINT UQ_Departments_Code UNIQUE (Code),
    CONSTRAINT FK_Departments_Parent FOREIGN KEY (ParentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_Departments_Organization FOREIGN KEY (OrganizationId) REFERENCES Organizations(OrganizationId)
);

CREATE TABLE LookupCategories (
    CategoryId      INT             NOT NULL IDENTITY(1,1),
    Code            NVARCHAR(100)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    IsSystem        BIT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT PK_LookupCategories PRIMARY KEY (CategoryId),
    CONSTRAINT UQ_LookupCategories_Code UNIQUE (Code)
);

CREATE TABLE LookupValues (
    ValueId         INT             NOT NULL IDENTITY(1,1),
    CategoryId      INT             NOT NULL,
    Code            NVARCHAR(100)   NOT NULL,
    ValueAr         NVARCHAR(300)   NOT NULL,
    ValueEn         NVARCHAR(300)   NOT NULL,
    Description     NVARCHAR(500)   NULL,
    SortOrder       INT             NOT NULL DEFAULT 0,
    IsDefault       BIT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT PK_LookupValues PRIMARY KEY (ValueId),
    CONSTRAINT FK_LookupValues_Category FOREIGN KEY (CategoryId) REFERENCES LookupCategories(CategoryId),
    CONSTRAINT UQ_LookupValues_Code UNIQUE (CategoryId, Code)
);

CREATE TABLE SystemSettings (
    SettingId       INT             NOT NULL IDENTITY(1,1),
    SettingKey      NVARCHAR(200)   NOT NULL,
    SettingValue    NVARCHAR(MAX)   NOT NULL,
    DataType        NVARCHAR(50)    NOT NULL DEFAULT 'String',
    GroupName       NVARCHAR(100)   NOT NULL DEFAULT 'General',
    DescriptionAr   NVARCHAR(500)   NULL,
    DescriptionEn   NVARCHAR(500)   NULL,
    IsEncrypted     BIT             NOT NULL DEFAULT 0,
    IsReadOnly      BIT             NOT NULL DEFAULT 0,
    UpdatedAt       DATETIME2       NULL,
    UpdatedBy       INT             NULL,
    CONSTRAINT PK_SystemSettings PRIMARY KEY (SettingId),
    CONSTRAINT UQ_SystemSettings_Key UNIQUE (SettingKey)
);

-- ============================================================
-- SECTION 2: IDENTITY & ACCESS MANAGEMENT
-- ============================================================

CREATE TABLE Roles (
    RoleId          INT             NOT NULL IDENTITY(1,1),
    RoleName        NVARCHAR(100)   NOT NULL,
    DisplayNameAr   NVARCHAR(200)   NOT NULL,
    DisplayNameEn   NVARCHAR(200)   NOT NULL,
    Description     NVARCHAR(500)   NULL,
    IsSystem        BIT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NULL,
    UpdatedAt       DATETIME2       NULL,
    IsDeleted       BIT             NOT NULL DEFAULT 0,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_Name UNIQUE (RoleName)
);

CREATE TABLE Permissions (
    PermissionId    INT             NOT NULL IDENTITY(1,1),
    PermissionCode  NVARCHAR(200)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    Module          NVARCHAR(100)   NOT NULL,
    Category        NVARCHAR(100)   NOT NULL,
    Description     NVARCHAR(500)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT PK_Permissions PRIMARY KEY (PermissionId),
    CONSTRAINT UQ_Permissions_Code UNIQUE (PermissionCode)
);

CREATE TABLE Users (
    UserId                  INT             NOT NULL IDENTITY(1,1),
    Username                NVARCHAR(100)   NOT NULL,
    Email                   NVARCHAR(256)   NOT NULL,
    PasswordHash            NVARCHAR(512)   NOT NULL,
    FullNameAr              NVARCHAR(200)   NOT NULL,
    FullNameEn              NVARCHAR(200)   NULL,
    DepartmentId            INT             NULL,
    JobTitle                NVARCHAR(200)   NULL,
    PhoneNumber             NVARCHAR(20)    NULL,
    IsActive                BIT             NOT NULL DEFAULT 1,
    IsLocked                BIT             NOT NULL DEFAULT 0,
    LockoutEnd              DATETIME2       NULL,
    FailedLoginAttempts     INT             NOT NULL DEFAULT 0,
    LastLoginAt             DATETIME2       NULL,
    LastLoginIP             NVARCHAR(45)    NULL,
    MFAEnabled              BIT             NOT NULL DEFAULT 0,
    MFASecret               NVARCHAR(256)   NULL,  -- Encrypted
    LanguagePreference      CHAR(2)         NOT NULL DEFAULT 'ar',
    ThemePreference         NVARCHAR(20)    NOT NULL DEFAULT 'light',
    AvatarPath              NVARCHAR(500)   NULL,
    ExternalId              NVARCHAR(256)   NULL,  -- AD GUID
    ExternalProvider        NVARCHAR(100)   NULL,  -- 'LDAP', 'OIDC', etc.
    MustChangePassword      BIT             NOT NULL DEFAULT 0,
    PasswordChangedAt       DATETIME2       NULL,
    CreatedAt               DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt               DATETIME2       NULL,
    CreatedBy               INT             NULL,
    UpdatedBy               INT             NULL,
    IsDeleted               BIT             NOT NULL DEFAULT 0,
    DeletedAt               DATETIME2       NULL,
    DeletedBy               INT             NULL,
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);

-- Add manager FK now that Users exists
ALTER TABLE Departments ADD CONSTRAINT FK_Departments_Manager FOREIGN KEY (ManagerId) REFERENCES Users(UserId);
ALTER TABLE Roles ADD CONSTRAINT FK_Roles_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId);
ALTER TABLE SystemSettings ADD CONSTRAINT FK_SystemSettings_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES Users(UserId);

CREATE TABLE UserRoles (
    UserRoleId      INT             NOT NULL IDENTITY(1,1),
    UserId          INT             NOT NULL,
    RoleId          INT             NOT NULL,
    AssignedAt      DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    AssignedBy      INT             NOT NULL,
    ExpiresAt       DATETIME2       NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserRoleId),
    CONSTRAINT UQ_UserRoles UNIQUE (UserId, RoleId),
    CONSTRAINT FK_UserRoles_User FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_UserRoles_Role FOREIGN KEY (RoleId) REFERENCES Roles(RoleId),
    CONSTRAINT FK_UserRoles_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES Users(UserId)
);

CREATE TABLE RolePermissions (
    RolePermissionId    INT         NOT NULL IDENTITY(1,1),
    RoleId              INT         NOT NULL,
    PermissionId        INT         NOT NULL,
    AssignedAt          DATETIME2   NOT NULL DEFAULT GETUTCDATE(),
    AssignedBy          INT         NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RolePermissionId),
    CONSTRAINT UQ_RolePermissions UNIQUE (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Role FOREIGN KEY (RoleId) REFERENCES Roles(RoleId),
    CONSTRAINT FK_RolePermissions_Permission FOREIGN KEY (PermissionId) REFERENCES Permissions(PermissionId),
    CONSTRAINT FK_RolePermissions_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES Users(UserId)
);

CREATE TABLE UserSessions (
    SessionId       NVARCHAR(100)   NOT NULL,
    UserId          INT             NOT NULL,
    RefreshToken    NVARCHAR(512)   NOT NULL,
    IPAddress       NVARCHAR(45)    NOT NULL,
    UserAgent       NVARCHAR(500)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt       DATETIME2       NOT NULL,
    LastActivityAt  DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    IsRevoked       BIT             NOT NULL DEFAULT 0,
    RevokedAt       DATETIME2       NULL,
    CONSTRAINT PK_UserSessions PRIMARY KEY (SessionId),
    CONSTRAINT FK_UserSessions_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
CREATE INDEX IX_UserSessions_UserId ON UserSessions(UserId);
CREATE INDEX IX_UserSessions_ExpiresAt ON UserSessions(ExpiresAt);

CREATE TABLE PasswordHistory (
    HistoryId       INT             NOT NULL IDENTITY(1,1),
    UserId          INT             NOT NULL,
    PasswordHash    NVARCHAR(512)   NOT NULL,
    ChangedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_PasswordHistory PRIMARY KEY (HistoryId),
    CONSTRAINT FK_PasswordHistory_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
CREATE INDEX IX_PasswordHistory_UserId ON PasswordHistory(UserId);

-- ============================================================
-- SECTION 3: CLASSIFICATION & SECURITY LEVELS
-- ============================================================

CREATE TABLE ClassificationLevels (
    LevelId         INT             NOT NULL IDENTITY(1,1),
    Code            NVARCHAR(50)    NOT NULL,
    NameAr          NVARCHAR(200)   NOT NULL,
    NameEn          NVARCHAR(200)   NOT NULL,
    LevelOrder      INT             NOT NULL,
    ColorCode       NVARCHAR(10)    NOT NULL DEFAULT '#000000',
    AllowDownload   BIT             NOT NULL DEFAULT 1,
    AllowPrint      BIT             NOT NULL DEFAULT 1,
    RequireWatermark BIT            NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT PK_ClassificationLevels PRIMARY KEY (LevelId),
    CONSTRAINT UQ_ClassificationLevels_Code UNIQUE (Code)
);

-- ============================================================
-- SECTION 4: DOCUMENT TYPES & METADATA ENGINE
-- ============================================================

CREATE TABLE DocumentTypes (
    TypeId          INT             NOT NULL IDENTITY(1,1),
    Code            NVARCHAR(100)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    IconClass       NVARCHAR(100)   NULL,
    DefaultRetentionYears INT       NULL,
    RequiresWorkflow BIT            NOT NULL DEFAULT 0,
    DefaultWorkflowId INT           NULL,  -- FK added later
    DefaultClassificationLevelId INT NULL,
    SortOrder       INT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    UpdatedAt       DATETIME2       NULL,
    IsDeleted       BIT             NOT NULL DEFAULT 0,
    CONSTRAINT PK_DocumentTypes PRIMARY KEY (TypeId),
    CONSTRAINT UQ_DocumentTypes_Code UNIQUE (Code),
    CONSTRAINT FK_DocumentTypes_Classification FOREIGN KEY (DefaultClassificationLevelId) REFERENCES ClassificationLevels(LevelId),
    CONSTRAINT FK_DocumentTypes_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE MetadataFields (
    FieldId         INT             NOT NULL IDENTITY(1,1),
    FieldCode       NVARCHAR(100)   NOT NULL,
    LabelAr         NVARCHAR(300)   NOT NULL,
    LabelEn         NVARCHAR(300)   NOT NULL,
    FieldType       NVARCHAR(50)    NOT NULL,  -- Text,Number,Date,Boolean,Lookup,MultiValue,LongText,Email,Url
    IsRequired      BIT             NOT NULL DEFAULT 0,
    IsSearchable    BIT             NOT NULL DEFAULT 1,
    IsMultiValue    BIT             NOT NULL DEFAULT 0,
    DefaultValue    NVARCHAR(500)   NULL,
    ValidationRegex NVARCHAR(500)   NULL,
    MinValue        NVARCHAR(100)   NULL,
    MaxValue        NVARCHAR(100)   NULL,
    MaxLength       INT             NULL,
    LookupCategoryId INT            NULL,
    PlaceholderAr   NVARCHAR(300)   NULL,
    PlaceholderEn   NVARCHAR(300)   NULL,
    HelpTextAr      NVARCHAR(500)   NULL,
    HelpTextEn      NVARCHAR(500)   NULL,
    SortOrder       INT             NOT NULL DEFAULT 0,
    IsSystem        BIT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_MetadataFields PRIMARY KEY (FieldId),
    CONSTRAINT UQ_MetadataFields_Code UNIQUE (FieldCode),
    CONSTRAINT FK_MetadataFields_LookupCategory FOREIGN KEY (LookupCategoryId) REFERENCES LookupCategories(CategoryId),
    CONSTRAINT FK_MetadataFields_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId),
    CONSTRAINT CHK_MetadataFields_Type CHECK (FieldType IN ('Text','Number','Date','Boolean','Lookup','MultiValue','LongText','Email','Url','RichText'))
);

CREATE TABLE DocumentTypeMetadataFields (
    Id              INT             NOT NULL IDENTITY(1,1),
    DocumentTypeId  INT             NOT NULL,
    FieldId         INT             NOT NULL,
    IsRequiredOverride BIT          NULL,  -- NULL = use FieldId default
    SortOrderOverride INT           NULL,
    GroupName       NVARCHAR(100)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT PK_DocumentTypeMetadataFields PRIMARY KEY (Id),
    CONSTRAINT UQ_DocTypeMetadata UNIQUE (DocumentTypeId, FieldId),
    CONSTRAINT FK_DocTypeMetadata_DocType FOREIGN KEY (DocumentTypeId) REFERENCES DocumentTypes(TypeId),
    CONSTRAINT FK_DocTypeMetadata_Field FOREIGN KEY (FieldId) REFERENCES MetadataFields(FieldId)
);

-- ============================================================
-- SECTION 5: RECORDS MANAGEMENT
-- ============================================================

CREATE TABLE RecordClasses (
    ClassId         INT             NOT NULL IDENTITY(1,1),
    ParentId        INT             NULL,
    Code            NVARCHAR(100)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    RetentionYears  INT             NULL,
    DisposalAction  NVARCHAR(50)    NOT NULL DEFAULT 'Delete',  -- Delete, Archive, Transfer, Review
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_RecordClasses PRIMARY KEY (ClassId),
    CONSTRAINT UQ_RecordClasses_Code UNIQUE (Code),
    CONSTRAINT FK_RecordClasses_Parent FOREIGN KEY (ParentId) REFERENCES RecordClasses(ClassId),
    CONSTRAINT FK_RecordClasses_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE RetentionPolicies (
    PolicyId        INT             NOT NULL IDENTITY(1,1),
    Code            NVARCHAR(100)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    RetentionYears  INT             NOT NULL,
    RetentionTrigger NVARCHAR(50)   NOT NULL DEFAULT 'CreationDate',  -- CreationDate, DocumentDate, LastModified, EventBased
    DisposalAction  NVARCHAR(50)    NOT NULL DEFAULT 'Delete',
    RequiresReview  BIT             NOT NULL DEFAULT 0,
    LegalReference  NVARCHAR(500)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    UpdatedAt       DATETIME2       NULL,
    CONSTRAINT PK_RetentionPolicies PRIMARY KEY (PolicyId),
    CONSTRAINT UQ_RetentionPolicies_Code UNIQUE (Code),
    CONSTRAINT FK_RetentionPolicies_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE LegalHolds (
    HoldId          INT             NOT NULL IDENTITY(1,1),
    HoldCode        NVARCHAR(100)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    Reason          NVARCHAR(2000)  NOT NULL,
    CaseReference   NVARCHAR(200)   NULL,
    StartDate       DATE            NOT NULL,
    EndDate         DATE            NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    ReleasedAt      DATETIME2       NULL,
    ReleasedBy      INT             NULL,
    CONSTRAINT PK_LegalHolds PRIMARY KEY (HoldId),
    CONSTRAINT UQ_LegalHolds_Code UNIQUE (HoldCode),
    CONSTRAINT FK_LegalHolds_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- ============================================================
-- SECTION 6: DOCUMENT LIBRARIES & STORAGE
-- ============================================================

CREATE TABLE DocumentLibraries (
    LibraryId           INT             NOT NULL IDENTITY(1,1),
    LibraryCode         NVARCHAR(50)    NOT NULL,
    NameAr              NVARCHAR(300)   NOT NULL,
    NameEn              NVARCHAR(300)   NOT NULL,
    Description         NVARCHAR(1000)  NULL,
    DepartmentId        INT             NULL,
    DefaultDocTypeId    INT             NULL,
    RetentionPolicyId   INT             NULL,
    StorageQuotaGB      DECIMAL(10,2)   NULL,
    IsPublic            BIT             NOT NULL DEFAULT 0,
    IconClass           NVARCHAR(100)   NULL,
    SortOrder           INT             NOT NULL DEFAULT 0,
    IsActive            BIT             NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           INT             NOT NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           INT             NULL,
    IsDeleted           BIT             NOT NULL DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    CONSTRAINT PK_DocumentLibraries PRIMARY KEY (LibraryId),
    CONSTRAINT UQ_DocumentLibraries_Code UNIQUE (LibraryCode),
    CONSTRAINT FK_DocLibraries_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_DocLibraries_DocType FOREIGN KEY (DefaultDocTypeId) REFERENCES DocumentTypes(TypeId),
    CONSTRAINT FK_DocLibraries_Retention FOREIGN KEY (RetentionPolicyId) REFERENCES RetentionPolicies(PolicyId),
    CONSTRAINT FK_DocLibraries_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE Folders (
    FolderId        INT             NOT NULL IDENTITY(1,1),
    ParentFolderId  INT             NULL,
    LibraryId       INT             NOT NULL,
    Code            NVARCHAR(100)   NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    Description     NVARCHAR(500)   NULL,
    PathAr          NVARCHAR(2000)  NULL,  -- Materialized path for breadcrumbs
    PathEn          NVARCHAR(2000)  NULL,
    DepthLevel      INT             NOT NULL DEFAULT 0,
    SortOrder       INT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    UpdatedAt       DATETIME2       NULL,
    IsDeleted       BIT             NOT NULL DEFAULT 0,
    DeletedAt       DATETIME2       NULL,
    CONSTRAINT PK_Folders PRIMARY KEY (FolderId),
    CONSTRAINT FK_Folders_Parent FOREIGN KEY (ParentFolderId) REFERENCES Folders(FolderId),
    CONSTRAINT FK_Folders_Library FOREIGN KEY (LibraryId) REFERENCES DocumentLibraries(LibraryId),
    CONSTRAINT FK_Folders_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);
CREATE INDEX IX_Folders_LibraryId ON Folders(LibraryId);
CREATE INDEX IX_Folders_ParentId ON Folders(ParentFolderId);

CREATE TABLE DocumentFiles (
    FileId          INT             NOT NULL IDENTITY(1,1),
    StorageKey      NVARCHAR(1000)  NOT NULL,
    OriginalFileName NVARCHAR(500)  NOT NULL,
    ContentType     NVARCHAR(200)   NOT NULL,
    FileExtension   NVARCHAR(20)    NOT NULL,
    FileSizeBytes   BIGINT          NOT NULL,
    ContentHash     NVARCHAR(64)    NOT NULL,
    ThumbnailKey    NVARCHAR(1000)  NULL,
    IsVirusScanPassed BIT           NULL,
    StorageProvider NVARCHAR(50)    NOT NULL DEFAULT 'LocalFileSystem',
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_DocumentFiles PRIMARY KEY (FileId),
    CONSTRAINT UQ_DocumentFiles_StorageKey UNIQUE (StorageKey),
    CONSTRAINT FK_DocumentFiles_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE Documents (
    DocumentId              UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    DocumentNumber          NVARCHAR(50)        NOT NULL,
    TitleAr                 NVARCHAR(500)       NOT NULL,
    TitleEn                 NVARCHAR(500)       NULL,
    DocumentTypeId          INT                 NOT NULL,
    LibraryId               INT                 NOT NULL,
    FolderId                INT                 NULL,
    RecordClassId           INT                 NULL,
    RetentionPolicyId       INT                 NULL,
    CurrentVersionId        INT                 NULL,  -- FK added after DocumentVersions
    StatusValueId           INT                 NOT NULL,  -- FK → LookupValues (DocStatus category)
    ClassificationLevelId   INT                 NOT NULL DEFAULT 1,
    CheckedOutBy            INT                 NULL,
    CheckedOutAt            DATETIME2           NULL,
    IsCheckedOut            BIT                 NOT NULL DEFAULT 0,
    IsLegalHold             BIT                 NOT NULL DEFAULT 0,
    RetentionExpiresAt      DATE                NULL,
    Keywords                NVARCHAR(2000)      NULL,
    Summary                 NVARCHAR(MAX)       NULL,
    SourceReference         NVARCHAR(500)       NULL,
    DocumentDate            DATE                NULL,
    ExpiryDate              DATE                NULL,
    PageCount               INT                 NULL,
    Language                CHAR(2)             NOT NULL DEFAULT 'ar',
    CreatedAt               DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy               INT                 NOT NULL,
    UpdatedAt               DATETIME2           NULL,
    UpdatedBy               INT                 NULL,
    IsDeleted               BIT                 NOT NULL DEFAULT 0,
    DeletedAt               DATETIME2           NULL,
    DeletedBy               INT                 NULL,
    CONSTRAINT PK_Documents PRIMARY KEY (DocumentId),
    CONSTRAINT UQ_Documents_Number UNIQUE (DocumentNumber),
    CONSTRAINT FK_Documents_DocType FOREIGN KEY (DocumentTypeId) REFERENCES DocumentTypes(TypeId),
    CONSTRAINT FK_Documents_Library FOREIGN KEY (LibraryId) REFERENCES DocumentLibraries(LibraryId),
    CONSTRAINT FK_Documents_Folder FOREIGN KEY (FolderId) REFERENCES Folders(FolderId),
    CONSTRAINT FK_Documents_RecordClass FOREIGN KEY (RecordClassId) REFERENCES RecordClasses(ClassId),
    CONSTRAINT FK_Documents_Retention FOREIGN KEY (RetentionPolicyId) REFERENCES RetentionPolicies(PolicyId),
    CONSTRAINT FK_Documents_Status FOREIGN KEY (StatusValueId) REFERENCES LookupValues(ValueId),
    CONSTRAINT FK_Documents_Classification FOREIGN KEY (ClassificationLevelId) REFERENCES ClassificationLevels(LevelId),
    CONSTRAINT FK_Documents_CheckedOutBy FOREIGN KEY (CheckedOutBy) REFERENCES Users(UserId),
    CONSTRAINT FK_Documents_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);
CREATE INDEX IX_Documents_DocumentNumber ON Documents(DocumentNumber);
CREATE INDEX IX_Documents_LibraryId ON Documents(LibraryId);
CREATE INDEX IX_Documents_FolderId ON Documents(FolderId);
CREATE INDEX IX_Documents_DocumentTypeId ON Documents(DocumentTypeId);
CREATE INDEX IX_Documents_StatusValueId ON Documents(StatusValueId);
CREATE INDEX IX_Documents_CreatedBy ON Documents(CreatedBy);
CREATE INDEX IX_Documents_RetentionExpiresAt ON Documents(RetentionExpiresAt) WHERE RetentionExpiresAt IS NOT NULL;
CREATE INDEX IX_Documents_IsDeleted ON Documents(IsDeleted);

CREATE TABLE DocumentVersions (
    VersionId       INT             NOT NULL IDENTITY(1,1),
    DocumentId      UNIQUEIDENTIFIER NOT NULL,
    VersionNumber   NVARCHAR(20)    NOT NULL,
    MajorVersion    INT             NOT NULL DEFAULT 1,
    MinorVersion    INT             NOT NULL DEFAULT 0,
    FileId          INT             NOT NULL,
    ChangeNote      NVARCHAR(1000)  NULL,
    CheckInNote     NVARCHAR(500)   NULL,
    IsCurrent       BIT             NOT NULL DEFAULT 1,
    FileSize        BIGINT          NOT NULL,
    FileName        NVARCHAR(500)   NOT NULL,
    ContentHash     NVARCHAR(64)    NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_DocumentVersions PRIMARY KEY (VersionId),
    CONSTRAINT FK_DocVersions_Document FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_DocVersions_File FOREIGN KEY (FileId) REFERENCES DocumentFiles(FileId),
    CONSTRAINT FK_DocVersions_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);
CREATE INDEX IX_DocVersions_DocumentId ON DocumentVersions(DocumentId);

-- Add CurrentVersionId FK now
ALTER TABLE Documents ADD CONSTRAINT FK_Documents_CurrentVersion FOREIGN KEY (CurrentVersionId) REFERENCES DocumentVersions(VersionId);

-- Document metadata values (EAV pattern)
CREATE TABLE DocumentMetadataValues (
    ValueId         BIGINT          NOT NULL IDENTITY(1,1),
    DocumentId      UNIQUEIDENTIFIER NOT NULL,
    FieldId         INT             NOT NULL,
    TextValue       NVARCHAR(MAX)   NULL,
    NumberValue     DECIMAL(18,4)   NULL,
    DateValue       DATETIME2       NULL,
    BoolValue       BIT             NULL,
    LookupValueId   INT             NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2       NULL,
    CONSTRAINT PK_DocumentMetadataValues PRIMARY KEY (ValueId),
    CONSTRAINT UQ_DocMetadata UNIQUE (DocumentId, FieldId),
    CONSTRAINT FK_DocMetadata_Document FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_DocMetadata_Field FOREIGN KEY (FieldId) REFERENCES MetadataFields(FieldId),
    CONSTRAINT FK_DocMetadata_Lookup FOREIGN KEY (LookupValueId) REFERENCES LookupValues(ValueId)
);
CREATE INDEX IX_DocMetadata_DocumentId ON DocumentMetadataValues(DocumentId);
CREATE INDEX IX_DocMetadata_FieldId ON DocumentMetadataValues(FieldId);

CREATE TABLE Tags (
    TagId           INT             NOT NULL IDENTITY(1,1),
    NameAr          NVARCHAR(200)   NOT NULL,
    NameEn          NVARCHAR(200)   NOT NULL,
    Slug            NVARCHAR(200)   NOT NULL,
    UsageCount      INT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_Tags PRIMARY KEY (TagId),
    CONSTRAINT UQ_Tags_Slug UNIQUE (Slug),
    CONSTRAINT FK_Tags_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE DocumentTags (
    Id              INT             NOT NULL IDENTITY(1,1),
    DocumentId      UNIQUEIDENTIFIER NOT NULL,
    TagId           INT             NOT NULL,
    AddedAt         DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    AddedBy         INT             NOT NULL,
    CONSTRAINT PK_DocumentTags PRIMARY KEY (Id),
    CONSTRAINT UQ_DocumentTags UNIQUE (DocumentId, TagId),
    CONSTRAINT FK_DocumentTags_Document FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_DocumentTags_Tag FOREIGN KEY (TagId) REFERENCES Tags(TagId),
    CONSTRAINT FK_DocumentTags_AddedBy FOREIGN KEY (AddedBy) REFERENCES Users(UserId)
);

CREATE TABLE DocumentLegalHolds (
    Id              INT             NOT NULL IDENTITY(1,1),
    DocumentId      UNIQUEIDENTIFIER NOT NULL,
    HoldId          INT             NOT NULL,
    AppliedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    AppliedBy       INT             NOT NULL,
    CONSTRAINT PK_DocumentLegalHolds PRIMARY KEY (Id),
    CONSTRAINT UQ_DocLegalHolds UNIQUE (DocumentId, HoldId),
    CONSTRAINT FK_DocLegalHolds_Document FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_DocLegalHolds_Hold FOREIGN KEY (HoldId) REFERENCES LegalHolds(HoldId),
    CONSTRAINT FK_DocLegalHolds_AppliedBy FOREIGN KEY (AppliedBy) REFERENCES Users(UserId)
);

CREATE TABLE DocumentLinks (
    LinkId          INT             NOT NULL IDENTITY(1,1),
    SourceDocumentId UNIQUEIDENTIFIER NOT NULL,
    TargetDocumentId UNIQUEIDENTIFIER NOT NULL,
    LinkType        NVARCHAR(100)   NOT NULL,  -- Related, Supersedes, References, Attachment
    Note            NVARCHAR(500)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_DocumentLinks PRIMARY KEY (LinkId),
    CONSTRAINT FK_DocLinks_Source FOREIGN KEY (SourceDocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_DocLinks_Target FOREIGN KEY (TargetDocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_DocLinks_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE DocumentComments (
    CommentId       INT             NOT NULL IDENTITY(1,1),
    DocumentId      UNIQUEIDENTIFIER NOT NULL,
    ParentCommentId INT             NULL,
    CommentText     NVARCHAR(MAX)   NOT NULL,
    IsPrivate       BIT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    UpdatedAt       DATETIME2       NULL,
    IsDeleted       BIT             NOT NULL DEFAULT 0,
    CONSTRAINT PK_DocumentComments PRIMARY KEY (CommentId),
    CONSTRAINT FK_DocComments_Document FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_DocComments_Parent FOREIGN KEY (ParentCommentId) REFERENCES DocumentComments(CommentId),
    CONSTRAINT FK_DocComments_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);
CREATE INDEX IX_DocComments_DocumentId ON DocumentComments(DocumentId);

-- ============================================================
-- SECTION 7: ACCESS CONTROL (FINE-GRAINED)
-- ============================================================

CREATE TABLE DocumentAccessPermissions (
    PermId          INT             NOT NULL IDENTITY(1,1),
    EntityType      NVARCHAR(50)    NOT NULL,   -- Library, Folder, Document
    EntityId        NVARCHAR(100)   NOT NULL,
    PrincipalType   NVARCHAR(50)    NOT NULL,   -- User, Role, Department
    PrincipalId     INT             NOT NULL,
    CanRead         BIT             NOT NULL DEFAULT 0,
    CanWrite        BIT             NOT NULL DEFAULT 0,
    CanDelete       BIT             NOT NULL DEFAULT 0,
    CanDownload     BIT             NOT NULL DEFAULT 0,
    CanPrint        BIT             NOT NULL DEFAULT 0,
    CanShare        BIT             NOT NULL DEFAULT 0,
    CanManage       BIT             NOT NULL DEFAULT 0,
    IsDeny          BIT             NOT NULL DEFAULT 0,  -- Explicit deny overrides allow
    GrantedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    GrantedBy       INT             NOT NULL,
    ExpiresAt       DATETIME2       NULL,
    CONSTRAINT PK_DocAccessPermissions PRIMARY KEY (PermId),
    CONSTRAINT FK_DocAccess_GrantedBy FOREIGN KEY (GrantedBy) REFERENCES Users(UserId)
);
CREATE INDEX IX_DocAccess_Entity ON DocumentAccessPermissions(EntityType, EntityId);
CREATE INDEX IX_DocAccess_Principal ON DocumentAccessPermissions(PrincipalType, PrincipalId);

-- ============================================================
-- SECTION 8: WORKFLOW ENGINE
-- ============================================================

CREATE TABLE WorkflowDefinitions (
    DefinitionId    INT             NOT NULL IDENTITY(1,1),
    Code            NVARCHAR(100)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    DocumentTypeId  INT             NULL,  -- NULL = applies to all types
    TriggerType     NVARCHAR(50)    NOT NULL DEFAULT 'Manual',  -- Manual, OnUpload, OnStatusChange
    Version         INT             NOT NULL DEFAULT 1,
    IsActive        BIT             NOT NULL DEFAULT 1,
    IsDefault       BIT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    UpdatedAt       DATETIME2       NULL,
    UpdatedBy       INT             NULL,
    IsDeleted       BIT             NOT NULL DEFAULT 0,
    CONSTRAINT PK_WorkflowDefinitions PRIMARY KEY (DefinitionId),
    CONSTRAINT UQ_WorkflowDefs_Code UNIQUE (Code),
    CONSTRAINT FK_WorkflowDefs_DocType FOREIGN KEY (DocumentTypeId) REFERENCES DocumentTypes(TypeId),
    CONSTRAINT FK_WorkflowDefs_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE TABLE WorkflowSteps (
    StepId          INT             NOT NULL IDENTITY(1,1),
    DefinitionId    INT             NOT NULL,
    StepOrder       INT             NOT NULL,
    StepCode        NVARCHAR(100)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    StepType        NVARCHAR(50)    NOT NULL DEFAULT 'Approval',  -- Approval, Review, Notification, Conditional
    AssigneeType    NVARCHAR(50)    NOT NULL,  -- SpecificUser, Role, Department, Dynamic, Sequential, Parallel
    AssigneeId      INT             NULL,
    AssigneeRoleId  INT             NULL,
    AssigneeDeptId  INT             NULL,
    DynamicFieldCode NVARCHAR(100)  NULL,  -- Metadata field that determines assignee
    SLAHours        INT             NULL,
    EscalationHours INT             NULL,
    EscalationUserId INT            NULL,
    AllowReject     BIT             NOT NULL DEFAULT 1,
    AllowReturn     BIT             NOT NULL DEFAULT 1,
    AllowDelegate   BIT             NOT NULL DEFAULT 1,
    RequireComment  BIT             NOT NULL DEFAULT 0,
    IsFirstStep     BIT             NOT NULL DEFAULT 0,
    IsFinalStep     BIT             NOT NULL DEFAULT 0,
    NotifyOnAssign  BIT             NOT NULL DEFAULT 1,
    InstructionAr   NVARCHAR(2000)  NULL,
    InstructionEn   NVARCHAR(2000)  NULL,
    CONSTRAINT PK_WorkflowSteps PRIMARY KEY (StepId),
    CONSTRAINT FK_WorkflowSteps_Definition FOREIGN KEY (DefinitionId) REFERENCES WorkflowDefinitions(DefinitionId),
    CONSTRAINT FK_WorkflowSteps_Assignee FOREIGN KEY (AssigneeId) REFERENCES Users(UserId),
    CONSTRAINT FK_WorkflowSteps_Role FOREIGN KEY (AssigneeRoleId) REFERENCES Roles(RoleId),
    CONSTRAINT FK_WorkflowSteps_Dept FOREIGN KEY (AssigneeDeptId) REFERENCES Departments(DepartmentId)
);
CREATE INDEX IX_WorkflowSteps_DefinitionId ON WorkflowSteps(DefinitionId);

CREATE TABLE WorkflowConditions (
    ConditionId     INT             NOT NULL IDENTITY(1,1),
    StepId          INT             NOT NULL,
    FieldCode       NVARCHAR(100)   NOT NULL,
    Operator        NVARCHAR(50)    NOT NULL,  -- Equals, NotEquals, GreaterThan, LessThan, Contains, IsEmpty
    ConditionValue  NVARCHAR(500)   NOT NULL,
    TargetStepId    INT             NOT NULL,
    SortOrder       INT             NOT NULL DEFAULT 0,
    CONSTRAINT PK_WorkflowConditions PRIMARY KEY (ConditionId),
    CONSTRAINT FK_WFConditions_Step FOREIGN KEY (StepId) REFERENCES WorkflowSteps(StepId),
    CONSTRAINT FK_WFConditions_TargetStep FOREIGN KEY (TargetStepId) REFERENCES WorkflowSteps(StepId)
);

CREATE TABLE WorkflowInstances (
    InstanceId      INT             NOT NULL IDENTITY(1,1),
    DefinitionId    INT             NOT NULL,
    DocumentId      UNIQUEIDENTIFIER NOT NULL,
    Status          NVARCHAR(50)    NOT NULL DEFAULT 'InProgress',  -- InProgress,Approved,Rejected,Returned,Cancelled,Archived
    CurrentStepId   INT             NULL,
    StartedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    StartedBy       INT             NOT NULL,
    CompletedAt     DATETIME2       NULL,
    CancelledAt     DATETIME2       NULL,
    CancelledBy     INT             NULL,
    CancellationReason NVARCHAR(500) NULL,
    Priority        INT             NOT NULL DEFAULT 2,  -- 1=Low, 2=Normal, 3=High, 4=Urgent
    DueDate         DATE            NULL,
    CONSTRAINT PK_WorkflowInstances PRIMARY KEY (InstanceId),
    CONSTRAINT FK_WFInstances_Definition FOREIGN KEY (DefinitionId) REFERENCES WorkflowDefinitions(DefinitionId),
    CONSTRAINT FK_WFInstances_Document FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_WFInstances_StartedBy FOREIGN KEY (StartedBy) REFERENCES Users(UserId)
);
CREATE INDEX IX_WFInstances_DocumentId ON WorkflowInstances(DocumentId);
CREATE INDEX IX_WFInstances_Status ON WorkflowInstances(Status);

CREATE TABLE WorkflowTasks (
    TaskId          INT             NOT NULL IDENTITY(1,1),
    InstanceId      INT             NOT NULL,
    StepId          INT             NOT NULL,
    AssignedToUserId INT            NULL,
    AssignedToRoleId INT            NULL,
    Status          NVARCHAR(50)    NOT NULL DEFAULT 'Pending',  -- Pending,Completed,Skipped,Expired
    AssignedAt      DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    DueAt           DATETIME2       NULL,
    CompletedAt     DATETIME2       NULL,
    CompletedBy     INT             NULL,
    IsOverdue       BIT             NOT NULL DEFAULT 0,
    IsDelegated     BIT             NOT NULL DEFAULT 0,
    DelegatedFrom   INT             NULL,
    SLABreachNotifiedAt DATETIME2   NULL,
    EscalatedAt     DATETIME2       NULL,
    CONSTRAINT PK_WorkflowTasks PRIMARY KEY (TaskId),
    CONSTRAINT FK_WFTasks_Instance FOREIGN KEY (InstanceId) REFERENCES WorkflowInstances(InstanceId),
    CONSTRAINT FK_WFTasks_Step FOREIGN KEY (StepId) REFERENCES WorkflowSteps(StepId),
    CONSTRAINT FK_WFTasks_User FOREIGN KEY (AssignedToUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_WFTasks_Role FOREIGN KEY (AssignedToRoleId) REFERENCES Roles(RoleId)
);
CREATE INDEX IX_WFTasks_InstanceId ON WorkflowTasks(InstanceId);
CREATE INDEX IX_WFTasks_AssignedUser ON WorkflowTasks(AssignedToUserId) WHERE AssignedToUserId IS NOT NULL;
CREATE INDEX IX_WFTasks_Status ON WorkflowTasks(Status);
CREATE INDEX IX_WFTasks_DueAt ON WorkflowTasks(DueAt) WHERE DueAt IS NOT NULL;

CREATE TABLE WorkflowActions (
    ActionId        INT             NOT NULL IDENTITY(1,1),
    TaskId          INT             NOT NULL,
    ActionType      NVARCHAR(50)    NOT NULL,  -- Approve, Reject, Return, Delegate, Escalate, Comment
    Comment         NVARCHAR(MAX)   NULL,
    ActionAt        DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    ActionBy        INT             NOT NULL,
    DelegatedToId   INT             NULL,
    CONSTRAINT PK_WorkflowActions PRIMARY KEY (ActionId),
    CONSTRAINT FK_WFActions_Task FOREIGN KEY (TaskId) REFERENCES WorkflowTasks(TaskId),
    CONSTRAINT FK_WFActions_ActionBy FOREIGN KEY (ActionBy) REFERENCES Users(UserId),
    CONSTRAINT FK_WFActions_DelegatedTo FOREIGN KEY (DelegatedToId) REFERENCES Users(UserId)
);
CREATE INDEX IX_WFActions_TaskId ON WorkflowActions(TaskId);

CREATE TABLE WorkflowDelegations (
    DelegationId    INT             NOT NULL IDENTITY(1,1),
    FromUserId      INT             NOT NULL,
    ToUserId        INT             NOT NULL,
    StartDate       DATE            NOT NULL,
    EndDate         DATE            NOT NULL,
    Reason          NVARCHAR(500)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_WorkflowDelegations PRIMARY KEY (DelegationId),
    CONSTRAINT FK_WFDelegations_FromUser FOREIGN KEY (FromUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_WFDelegations_ToUser FOREIGN KEY (ToUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_WFDelegations_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- Add DefaultWorkflowId FK to DocumentTypes now
ALTER TABLE DocumentTypes ADD CONSTRAINT FK_DocTypes_DefaultWorkflow FOREIGN KEY (DefaultWorkflowId) REFERENCES WorkflowDefinitions(DefinitionId);

-- ============================================================
-- SECTION 9: SEARCH
-- ============================================================

CREATE TABLE SavedSearches (
    SearchId        INT             NOT NULL IDENTITY(1,1),
    UserId          INT             NOT NULL,
    NameAr          NVARCHAR(200)   NOT NULL,
    NameEn          NVARCHAR(200)   NULL,
    QueryJson       NVARCHAR(MAX)   NOT NULL,  -- JSON-serialized search parameters
    IsPublic        BIT             NOT NULL DEFAULT 0,
    LastRunAt       DATETIME2       NULL,
    RunCount        INT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_SavedSearches PRIMARY KEY (SearchId),
    CONSTRAINT FK_SavedSearches_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
CREATE INDEX IX_SavedSearches_UserId ON SavedSearches(UserId);

-- ============================================================
-- SECTION 10: NOTIFICATIONS
-- ============================================================

CREATE TABLE NotificationTemplates (
    TemplateId      INT             NOT NULL IDENTITY(1,1),
    Code            NVARCHAR(100)   NOT NULL,
    EventType       NVARCHAR(100)   NOT NULL,
    SubjectAr       NVARCHAR(500)   NULL,
    SubjectEn       NVARCHAR(500)   NULL,
    BodyAr          NVARCHAR(MAX)   NOT NULL,
    BodyEn          NVARCHAR(MAX)   NOT NULL,
    IsEmail         BIT             NOT NULL DEFAULT 1,
    IsInApp         BIT             NOT NULL DEFAULT 1,
    IsSMS           BIT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT PK_NotificationTemplates PRIMARY KEY (TemplateId),
    CONSTRAINT UQ_NotifTemplates_Code UNIQUE (Code)
);

CREATE TABLE Notifications (
    NotificationId  BIGINT          NOT NULL IDENTITY(1,1),
    UserId          INT             NOT NULL,
    Title           NVARCHAR(500)   NOT NULL,
    Body            NVARCHAR(MAX)   NOT NULL,
    NotificationType NVARCHAR(100)  NOT NULL,
    EntityType      NVARCHAR(100)   NULL,
    EntityId        NVARCHAR(100)   NULL,
    ActionUrl       NVARCHAR(500)   NULL,
    Priority        INT             NOT NULL DEFAULT 2,
    IsRead          BIT             NOT NULL DEFAULT 0,
    ReadAt          DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt       DATETIME2       NULL,
    CONSTRAINT PK_Notifications PRIMARY KEY (NotificationId),
    CONSTRAINT FK_Notifications_User FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);
CREATE INDEX IX_Notifications_IsRead ON Notifications(UserId, IsRead);
CREATE INDEX IX_Notifications_CreatedAt ON Notifications(CreatedAt DESC);

-- ============================================================
-- SECTION 11: AUDIT LOG (APPEND-ONLY)
-- ============================================================

CREATE TABLE AuditLogs (
    AuditId         BIGINT          NOT NULL IDENTITY(1,1),
    EventType       NVARCHAR(100)   NOT NULL,
    EntityType      NVARCHAR(100)   NULL,
    EntityId        NVARCHAR(100)   NULL,
    UserId          INT             NULL,
    Username        NVARCHAR(100)   NULL,  -- Denormalized for log integrity
    SessionId       NVARCHAR(100)   NULL,
    IPAddress       NVARCHAR(45)    NULL,
    UserAgent       NVARCHAR(500)   NULL,
    OldValues       NVARCHAR(MAX)   NULL,  -- JSON
    NewValues       NVARCHAR(MAX)   NULL,  -- JSON
    AdditionalInfo  NVARCHAR(MAX)   NULL,  -- JSON
    Severity        NVARCHAR(20)    NOT NULL DEFAULT 'Info',
    IsSuccessful    BIT             NOT NULL DEFAULT 1,
    FailureReason   NVARCHAR(500)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_AuditLogs PRIMARY KEY (AuditId)
);
CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt DESC);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId) WHERE UserId IS NOT NULL;
CREATE INDEX IX_AuditLogs_EventType ON AuditLogs(EventType);
CREATE INDEX IX_AuditLogs_EntityType ON AuditLogs(EntityType, EntityId);

-- ============================================================
-- SECTION 12: REPORTING
-- ============================================================

CREATE TABLE ReportDefinitions (
    ReportId        INT             NOT NULL IDENTITY(1,1),
    Code            NVARCHAR(100)   NOT NULL,
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    Category        NVARCHAR(100)   NOT NULL,
    QueryDefinition NVARCHAR(MAX)   NOT NULL,  -- JSON
    ParametersSchema NVARCHAR(MAX)  NULL,       -- JSON schema for params
    RequiredPermission NVARCHAR(200) NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_ReportDefinitions PRIMARY KEY (ReportId),
    CONSTRAINT UQ_ReportDefs_Code UNIQUE (Code),
    CONSTRAINT FK_ReportDefs_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- ============================================================
-- SECTION 13: FULL-TEXT SEARCH INDEXES
-- ============================================================

CREATE FULLTEXT INDEX ON Documents(TitleAr, TitleEn, Keywords, Summary)
    KEY INDEX PK_Documents ON ECM_FTS_Catalog;

CREATE FULLTEXT INDEX ON DocumentMetadataValues(TextValue)
    KEY INDEX PK_DocumentMetadataValues ON ECM_FTS_Catalog;

-- ============================================================
-- SECTION 14: SEED DATA
-- ============================================================

-- Languages
INSERT INTO Languages (Code, NameAr, NameEn, IsDefault) VALUES
('ar', 'العربية', 'Arabic', 1),
('en', 'الإنجليزية', 'English', 0);

-- Organization
INSERT INTO Organizations (Code, NameAr, NameEn) VALUES
('DARAH', 'دارة الملك عبدالعزيز', 'King Abdulaziz Foundation');

-- Classification Levels
INSERT INTO ClassificationLevels (Code, NameAr, NameEn, LevelOrder, ColorCode, AllowDownload, AllowPrint, RequireWatermark) VALUES
('PUBLIC',       'عام',      'Public',         1, '#22C55E', 1, 1, 0),
('INTERNAL',     'داخلي',    'Internal',       2, '#3B82F6', 1, 1, 0),
('CONFIDENTIAL', 'سري',      'Confidential',   3, '#F59E0B', 1, 1, 1),
('SECRET',       'سري للغاية','Secret',         4, '#EF4444', 0, 0, 1);

-- Lookup Categories
INSERT INTO LookupCategories (Code, NameAr, NameEn, IsSystem) VALUES
('DOC_STATUS',   'حالة الوثيقة',     'Document Status',   1),
('DOC_PRIORITY', 'الأولوية',          'Priority',          1),
('WF_STATUS',    'حالة سير العمل',   'Workflow Status',   1),
('DISPOSAL_TYPE','نوع الإتلاف',       'Disposal Type',     1);

-- Document Status Values
INSERT INTO LookupValues (CategoryId, Code, ValueAr, ValueEn, SortOrder) VALUES
(1, 'DRAFT',      'مسودة',        'Draft',      1),
(1, 'ACTIVE',     'نشط',          'Active',     2),
(1, 'PENDING',    'في الانتظار',  'Pending',    3),
(1, 'APPROVED',   'معتمد',        'Approved',   4),
(1, 'REJECTED',   'مرفوض',        'Rejected',   5),
(1, 'ARCHIVED',   'مؤرشف',        'Archived',   6),
(1, 'SUPERSEDED', 'محل آخر',      'Superseded', 7),
(1, 'DISPOSED',   'تم الإتلاف',   'Disposed',   8);

-- System Roles
INSERT INTO Roles (RoleName, DisplayNameAr, DisplayNameEn, IsSystem) VALUES
('SystemAdmin',       'مدير النظام',              'System Administrator',      1),
('ContentManager',    'مدير المحتوى',             'Content Manager',           1),
('DocumentManager',   'مدير الوثائق',             'Document Manager',          1),
('WorkflowApprover',  'معتمد سير العمل',          'Workflow Approver',         1),
('RecordsManager',    'مدير السجلات',             'Records Manager',           1),
('AuditReviewer',     'مراجع التدقيق',            'Audit Reviewer',            1),
('BasicUser',         'مستخدم أساسي',             'Basic User',                1),
('ReadOnly',          'قراءة فقط',                'Read Only',                 1);

-- Core Permissions
INSERT INTO Permissions (PermissionCode, NameAr, NameEn, Module, Category) VALUES
-- Documents
('documents.read',           'عرض الوثائق',              'View Documents',           'Documents', 'Read'),
('documents.create',         'إنشاء الوثائق',            'Create Documents',         'Documents', 'Write'),
('documents.update',         'تعديل الوثائق',            'Edit Documents',           'Documents', 'Write'),
('documents.delete',         'حذف الوثائق',              'Delete Documents',         'Documents', 'Delete'),
('documents.download',       'تحميل الوثائق',            'Download Documents',       'Documents', 'Action'),
('documents.print',          'طباعة الوثائق',            'Print Documents',          'Documents', 'Action'),
('documents.checkin',        'إيداع الوثائق',            'Check In Documents',       'Documents', 'Action'),
('documents.checkout',       'سحب الوثائق',              'Check Out Documents',      'Documents', 'Action'),
-- Workflows
('workflow.submit',          'إرسال لسير العمل',         'Submit to Workflow',       'Workflow',  'Action'),
('workflow.approve',         'اعتماد',                   'Approve',                  'Workflow',  'Action'),
('workflow.reject',          'رفض',                      'Reject',                   'Workflow',  'Action'),
('workflow.delegate',        'تفويض',                    'Delegate',                 'Workflow',  'Action'),
('workflow.manage',          'إدارة تعريفات سير العمل', 'Manage Workflow Defs',     'Workflow',  'Admin'),
-- Administration
('admin.users',              'إدارة المستخدمين',         'Manage Users',             'Admin',     'Admin'),
('admin.roles',              'إدارة الأدوار',            'Manage Roles',             'Admin',     'Admin'),
('admin.doctypes',           'إدارة أنواع الوثائق',      'Manage Document Types',    'Admin',     'Admin'),
('admin.metadata',           'إدارة البيانات الوصفية',   'Manage Metadata Fields',   'Admin',     'Admin'),
('admin.retention',          'إدارة سياسات الاحتفاظ',   'Manage Retention',         'Admin',     'Admin'),
('admin.system',             'إعدادات النظام',           'System Settings',          'Admin',     'Admin'),
-- Audit & Reports
('audit.read',               'عرض سجل التدقيق',          'View Audit Logs',          'Audit',     'Read'),
('audit.export',             'تصدير سجل التدقيق',        'Export Audit Logs',        'Audit',     'Action'),
('reports.view',             'عرض التقارير',             'View Reports',             'Reports',   'Read'),
('reports.export',           'تصدير التقارير',           'Export Reports',           'Reports',   'Action');

-- System Settings
INSERT INTO SystemSettings (SettingKey, SettingValue, DataType, GroupName, DescriptionAr, DescriptionEn) VALUES
('system.name.ar',                'نظام إدارة المحتوى المؤسسي', 'String', 'General', 'اسم النظام بالعربية', 'System Arabic Name'),
('system.name.en',                'DARAH ECM', 'String', 'General', 'اسم النظام بالإنجليزية', 'System English Name'),
('auth.session.timeout.minutes',  '480', 'Integer', 'Security', 'مهلة انتهاء الجلسة بالدقائق', 'Session timeout in minutes'),
('auth.max.failed.attempts',      '5', 'Integer', 'Security', 'الحد الأقصى لمحاولات الدخول الفاشلة', 'Max failed login attempts'),
('auth.lockout.duration.minutes', '30', 'Integer', 'Security', 'مدة الإيقاف بالدقائق', 'Account lockout duration'),
('auth.password.min.length',      '10', 'Integer', 'Security', 'الحد الأدنى لطول كلمة المرور', 'Minimum password length'),
('auth.password.history.count',   '12', 'Integer', 'Security', 'عدد كلمات المرور المحفوظة في التاريخ', 'Password history count'),
('auth.jwt.expiry.minutes',       '15', 'Integer', 'Security', 'مدة صلاحية JWT بالدقائق', 'JWT token expiry in minutes'),
('auth.refresh.expiry.hours',     '8', 'Integer', 'Security', 'مدة صلاحية Refresh Token بالساعات', 'Refresh token expiry in hours'),
('storage.max.file.size.mb',      '512', 'Integer', 'Storage', 'الحجم الأقصى للملف بالميغابايت', 'Max file size in MB'),
('storage.allowed.extensions',    '.pdf,.docx,.xlsx,.pptx,.txt,.jpg,.jpeg,.png,.tif,.tiff,.mp4,.mp3,.zip', 'String', 'Storage', 'الامتدادات المسموح بها', 'Allowed file extensions'),
('notifications.email.enabled',   'true', 'Boolean', 'Notifications', 'تفعيل إشعارات البريد الإلكتروني', 'Enable email notifications'),
('workflow.sla.check.interval.minutes', '15', 'Integer', 'Workflow', 'فترة فحص SLA بالدقائق', 'SLA check interval in minutes');

GO

-- ============================================================
-- SECTION 15: VIEWS FOR COMMON QUERIES
-- ============================================================

CREATE VIEW vw_DocumentSummary AS
SELECT
    d.DocumentId,
    d.DocumentNumber,
    d.TitleAr,
    d.TitleEn,
    dt.NameAr AS DocumentTypeAr,
    dt.NameEn AS DocumentTypeEn,
    dl.NameAr AS LibraryAr,
    dl.NameEn AS LibraryEn,
    f.NameAr AS FolderAr,
    lv.ValueAr AS StatusAr,
    lv.ValueEn AS StatusEn,
    cl.NameAr AS ClassificationAr,
    cl.LevelOrder AS ClassificationLevel,
    dv.VersionNumber AS CurrentVersion,
    df.FileSizeBytes,
    df.FileExtension,
    df.ContentType,
    d.IsCheckedOut,
    d.IsLegalHold,
    d.RetentionExpiresAt,
    d.DocumentDate,
    d.CreatedAt,
    u.FullNameAr AS CreatedByAr,
    u.FullNameEn AS CreatedByEn,
    d.UpdatedAt,
    d.IsDeleted
FROM Documents d
LEFT JOIN DocumentTypes dt ON d.DocumentTypeId = dt.TypeId
LEFT JOIN DocumentLibraries dl ON d.LibraryId = dl.LibraryId
LEFT JOIN Folders f ON d.FolderId = f.FolderId
LEFT JOIN LookupValues lv ON d.StatusValueId = lv.ValueId
LEFT JOIN ClassificationLevels cl ON d.ClassificationLevelId = cl.LevelId
LEFT JOIN DocumentVersions dv ON d.CurrentVersionId = dv.VersionId
LEFT JOIN DocumentFiles df ON dv.FileId = df.FileId
LEFT JOIN Users u ON d.CreatedBy = u.UserId
WHERE d.IsDeleted = 0;
GO

CREATE VIEW vw_WorkflowInbox AS
SELECT
    wt.TaskId,
    wi.InstanceId,
    wi.DocumentId,
    ds.TitleAr AS DocumentTitleAr,
    ds.TitleEn AS DocumentTitleEn,
    ds.DocumentTypeAr,
    ds.LibraryAr,
    wd.NameAr AS WorkflowNameAr,
    wd.NameEn AS WorkflowNameEn,
    ws.NameAr AS StepNameAr,
    ws.NameEn AS StepNameEn,
    wt.AssignedToUserId,
    wt.AssignedToRoleId,
    wt.Status,
    wt.AssignedAt,
    wt.DueAt,
    wt.IsOverdue,
    wt.IsDelegated,
    wi.Priority,
    su.FullNameAr AS SubmittedByAr,
    wi.StartedAt AS SubmittedAt
FROM WorkflowTasks wt
INNER JOIN WorkflowInstances wi ON wt.InstanceId = wi.InstanceId
INNER JOIN WorkflowDefinitions wd ON wi.DefinitionId = wd.DefinitionId
INNER JOIN WorkflowSteps ws ON wt.StepId = ws.StepId
LEFT JOIN vw_DocumentSummary ds ON wi.DocumentId = ds.DocumentId
LEFT JOIN Users su ON wi.StartedBy = su.UserId
WHERE wt.Status = 'Pending';
GO

-- ============================================================
-- END OF SCHEMA
-- ============================================================
PRINT 'DARAH ECM Schema created successfully.';
GO
