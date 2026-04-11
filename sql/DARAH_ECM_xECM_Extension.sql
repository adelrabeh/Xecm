-- ============================================================
-- DARAH ECM v1.1 — xECM Extension: Business Workspace Layer
-- SQL Server Schema Addendum
-- Run AFTER DARAH_ECM_Schema.sql (v1.0)
-- ============================================================

USE DARAH_ECM;
GO

-- ============================================================
-- SECTION 1: WORKSPACE TYPES
-- ============================================================

CREATE TABLE WorkspaceTypes (
    TypeId              INT             NOT NULL IDENTITY(1,1),
    Code                NVARCHAR(100)   NOT NULL,
    NameAr              NVARCHAR(300)   NOT NULL,
    NameEn              NVARCHAR(300)   NOT NULL,
    Description         NVARCHAR(1000)  NULL,
    IconClass           NVARCHAR(100)   NULL,
    -- Behavior flags
    AutoCreateOnExternal BIT            NOT NULL DEFAULT 0,  -- create workspace when external object arrives
    InheritRetention    BIT             NOT NULL DEFAULT 1,
    InheritSecurity     BIT             NOT NULL DEFAULT 1,
    InheritWorkflow     BIT             NOT NULL DEFAULT 0,
    -- External system binding
    DefaultExternalSystem NVARCHAR(100) NULL,               -- 'SAP', 'CRM', 'HR', etc.
    ExternalObjectType  NVARCHAR(200)   NULL,               -- e.g. 'SAPProject', 'SFAccount'
    -- Records
    DefaultRetentionPolicyId INT        NULL,
    DefaultClassificationLevelId INT    NULL DEFAULT 1,
    SortOrder           INT             NOT NULL DEFAULT 0,
    IsActive            BIT             NOT NULL DEFAULT 1,
    IsSystem            BIT             NOT NULL DEFAULT 0,
    CreatedAt           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           INT             NOT NULL,
    IsDeleted           BIT             NOT NULL DEFAULT 0,
    CONSTRAINT PK_WorkspaceTypes PRIMARY KEY (TypeId),
    CONSTRAINT UQ_WorkspaceTypes_Code UNIQUE (Code),
    CONSTRAINT FK_WSTypes_Retention FOREIGN KEY (DefaultRetentionPolicyId) REFERENCES RetentionPolicies(PolicyId),
    CONSTRAINT FK_WSTypes_Classification FOREIGN KEY (DefaultClassificationLevelId) REFERENCES ClassificationLevels(LevelId),
    CONSTRAINT FK_WSTypes_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- ============================================================
-- SECTION 2: WORKSPACES (Core entity)
-- ============================================================

CREATE TABLE Workspaces (
    WorkspaceId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    WorkspaceNumber     NVARCHAR(50)    NOT NULL,
    WorkspaceTypeId     INT             NOT NULL,
    TitleAr             NVARCHAR(500)   NOT NULL,
    TitleEn             NVARCHAR(500)   NULL,
    Description         NVARCHAR(MAX)   NULL,
    -- Business context
    OwnerId             INT             NOT NULL,
    DepartmentId        INT             NULL,
    -- External system binding (xECM core)
    ExternalSystemId    NVARCHAR(100)   NULL,   -- e.g. 'SAP_PROD', 'CRM_SF', 'HR_ORACLE'
    ExternalObjectId    NVARCHAR(200)   NULL,   -- e.g. SAP project ID, CRM account ID
    ExternalObjectType  NVARCHAR(200)   NULL,   -- e.g. 'Project', 'Contract', 'Case'
    ExternalObjectUrl   NVARCHAR(1000)  NULL,   -- Deep link back to external system
    LastSyncedAt        DATETIME2       NULL,   -- Last successful metadata sync
    SyncStatus          NVARCHAR(50)    NULL,   -- Pending, Synced, Failed, Conflict
    SyncError           NVARCHAR(1000)  NULL,
    -- Status & lifecycle
    StatusValueId       INT             NOT NULL,    -- FK → LookupValues (WS_STATUS category)
    ClassificationLevelId INT           NOT NULL DEFAULT 1,
    IsLegalHold         BIT             NOT NULL DEFAULT 0,
    RetentionPolicyId   INT             NULL,
    RetentionExpiresAt  DATE            NULL,
    ArchivedAt          DATETIME2       NULL,
    ArchivedBy          INT             NULL,
    DisposedAt          DATETIME2       NULL,
    DisposedBy          INT             NULL,
    -- Audit
    CreatedAt           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           INT             NOT NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           INT             NULL,
    IsDeleted           BIT             NOT NULL DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           INT             NULL,
    CONSTRAINT PK_Workspaces PRIMARY KEY (WorkspaceId),
    CONSTRAINT UQ_Workspaces_Number UNIQUE (WorkspaceNumber),
    CONSTRAINT FK_Workspaces_Type FOREIGN KEY (WorkspaceTypeId) REFERENCES WorkspaceTypes(TypeId),
    CONSTRAINT FK_Workspaces_Owner FOREIGN KEY (OwnerId) REFERENCES Users(UserId),
    CONSTRAINT FK_Workspaces_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_Workspaces_Status FOREIGN KEY (StatusValueId) REFERENCES LookupValues(ValueId),
    CONSTRAINT FK_Workspaces_Classification FOREIGN KEY (ClassificationLevelId) REFERENCES ClassificationLevels(LevelId),
    CONSTRAINT FK_Workspaces_Retention FOREIGN KEY (RetentionPolicyId) REFERENCES RetentionPolicies(PolicyId),
    CONSTRAINT FK_Workspaces_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

CREATE INDEX IX_Workspaces_TypeId        ON Workspaces(WorkspaceTypeId);
CREATE INDEX IX_Workspaces_OwnerId       ON Workspaces(OwnerId);
CREATE INDEX IX_Workspaces_DeptId        ON Workspaces(DepartmentId) WHERE DepartmentId IS NOT NULL;
CREATE INDEX IX_Workspaces_External      ON Workspaces(ExternalSystemId, ExternalObjectId) WHERE ExternalSystemId IS NOT NULL;
CREATE INDEX IX_Workspaces_Status        ON Workspaces(StatusValueId);
CREATE INDEX IX_Workspaces_IsDeleted     ON Workspaces(IsDeleted);
CREATE INDEX IX_Workspaces_SyncStatus    ON Workspaces(SyncStatus) WHERE SyncStatus IS NOT NULL;

-- ============================================================
-- SECTION 3: WORKSPACE METADATA (Dynamic, EAV)
-- ============================================================

CREATE TABLE WorkspaceMetadataValues (
    ValueId         BIGINT              NOT NULL IDENTITY(1,1),
    WorkspaceId     UNIQUEIDENTIFIER    NOT NULL,
    FieldId         INT                 NOT NULL,   -- FK → MetadataFields (reuse existing engine)
    TextValue       NVARCHAR(MAX)       NULL,
    NumberValue     DECIMAL(18,4)       NULL,
    DateValue       DATETIME2           NULL,
    BoolValue       BIT                 NULL,
    LookupValueId   INT                 NULL,
    -- Sync tracking: was this value set by external sync or by user?
    SourceType      NVARCHAR(50)        NOT NULL DEFAULT 'Manual',  -- Manual, ExternalSync, Inherited
    ExternalFieldRef NVARCHAR(200)      NULL,   -- original field name in external system
    LastSyncedAt    DATETIME2           NULL,
    CreatedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2           NULL,
    CONSTRAINT PK_WorkspaceMetadataValues PRIMARY KEY (ValueId),
    CONSTRAINT UQ_WSMetadata UNIQUE (WorkspaceId, FieldId),
    CONSTRAINT FK_WSMetadata_Workspace FOREIGN KEY (WorkspaceId) REFERENCES Workspaces(WorkspaceId),
    CONSTRAINT FK_WSMetadata_Field FOREIGN KEY (FieldId) REFERENCES MetadataFields(FieldId),
    CONSTRAINT FK_WSMetadata_Lookup FOREIGN KEY (LookupValueId) REFERENCES LookupValues(ValueId),
    CONSTRAINT CHK_WSMetadata_Source CHECK (SourceType IN ('Manual','ExternalSync','Inherited','Computed'))
);
CREATE INDEX IX_WSMetadata_WorkspaceId ON WorkspaceMetadataValues(WorkspaceId);
CREATE INDEX IX_WSMetadata_FieldId     ON WorkspaceMetadataValues(FieldId);

-- ============================================================
-- SECTION 4: WORKSPACE ↔ DOCUMENT BINDING
-- ============================================================

CREATE TABLE WorkspaceDocuments (
    Id              BIGINT              NOT NULL IDENTITY(1,1),
    WorkspaceId     UNIQUEIDENTIFIER    NOT NULL,
    DocumentId      UNIQUEIDENTIFIER    NOT NULL,
    BindingType     NVARCHAR(50)        NOT NULL DEFAULT 'Primary',  -- Primary, Reference, Attachment
    IsInherited     BIT                 NOT NULL DEFAULT 0,   -- inherited from parent workspace
    AddedAt         DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    AddedBy         INT                 NOT NULL,
    RemovedAt       DATETIME2           NULL,
    RemovedBy       INT                 NULL,
    IsActive        BIT                 NOT NULL DEFAULT 1,
    CONSTRAINT PK_WorkspaceDocuments PRIMARY KEY (Id),
    CONSTRAINT UQ_WSDocuments UNIQUE (WorkspaceId, DocumentId),
    CONSTRAINT FK_WSDocs_Workspace FOREIGN KEY (WorkspaceId) REFERENCES Workspaces(WorkspaceId),
    CONSTRAINT FK_WSDocs_Document FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId),
    CONSTRAINT FK_WSDocs_AddedBy FOREIGN KEY (AddedBy) REFERENCES Users(UserId),
    CONSTRAINT CHK_WSDocs_BindingType CHECK (BindingType IN ('Primary','Reference','Attachment','Supporting'))
);
CREATE INDEX IX_WSDocs_WorkspaceId ON WorkspaceDocuments(WorkspaceId);
CREATE INDEX IX_WSDocs_DocumentId  ON WorkspaceDocuments(DocumentId);

-- ============================================================
-- SECTION 5: WORKSPACE SECURITY POLICIES
-- ============================================================

CREATE TABLE WorkspaceSecurityPolicies (
    PolicyId        INT                 NOT NULL IDENTITY(1,1),
    WorkspaceId     UNIQUEIDENTIFIER    NOT NULL,
    PrincipalType   NVARCHAR(50)        NOT NULL,   -- User, Role, Department
    PrincipalId     INT                 NOT NULL,
    CanRead         BIT                 NOT NULL DEFAULT 0,
    CanWrite        BIT                 NOT NULL DEFAULT 0,
    CanDelete       BIT                 NOT NULL DEFAULT 0,
    CanDownload     BIT                 NOT NULL DEFAULT 0,
    CanManage       BIT                 NOT NULL DEFAULT 0,
    IsDeny          BIT                 NOT NULL DEFAULT 0,
    InheritToDocuments BIT              NOT NULL DEFAULT 1,  -- push down to all documents
    GrantedAt       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
    GrantedBy       INT                 NOT NULL,
    ExpiresAt       DATETIME2           NULL,
    CONSTRAINT PK_WorkspaceSecurityPolicies PRIMARY KEY (PolicyId),
    CONSTRAINT FK_WSPolicy_Workspace FOREIGN KEY (WorkspaceId) REFERENCES Workspaces(WorkspaceId),
    CONSTRAINT FK_WSPolicy_GrantedBy FOREIGN KEY (GrantedBy) REFERENCES Users(UserId)
);
CREATE INDEX IX_WSPolicy_WorkspaceId  ON WorkspaceSecurityPolicies(WorkspaceId);
CREATE INDEX IX_WSPolicy_Principal    ON WorkspaceSecurityPolicies(PrincipalType, PrincipalId);

-- ============================================================
-- SECTION 6: WORKSPACE WORKFLOW TEMPLATES
-- ============================================================

CREATE TABLE WorkspaceWorkflowTemplates (
    Id              INT                 NOT NULL IDENTITY(1,1),
    WorkspaceTypeId INT                 NOT NULL,
    WorkflowDefinitionId INT            NOT NULL,
    TriggerEvent    NVARCHAR(100)       NOT NULL,  -- DocumentAdded, WorkspaceCreated, StatusChanged, MetadataSynced
    IsAutomatic     BIT                 NOT NULL DEFAULT 0,
    IsActive        BIT                 NOT NULL DEFAULT 1,
    SortOrder       INT                 NOT NULL DEFAULT 0,
    CONSTRAINT PK_WSWorkflowTemplates PRIMARY KEY (Id),
    CONSTRAINT FK_WSWFTemplate_Type FOREIGN KEY (WorkspaceTypeId) REFERENCES WorkspaceTypes(TypeId),
    CONSTRAINT FK_WSWFTemplate_Workflow FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(DefinitionId)
);

-- ============================================================
-- SECTION 7: EXTERNAL SYSTEM REGISTRY
-- ============================================================

CREATE TABLE ExternalSystems (
    SystemId        INT             NOT NULL IDENTITY(1,1),
    SystemCode      NVARCHAR(100)   NOT NULL,   -- 'SAP_PROD', 'SF_CRM', 'ORACLE_HR'
    NameAr          NVARCHAR(300)   NOT NULL,
    NameEn          NVARCHAR(300)   NOT NULL,
    SystemType      NVARCHAR(100)   NOT NULL,   -- SAP, Salesforce, Oracle, Dynamics, Custom
    BaseUrl         NVARCHAR(1000)  NULL,        -- API base URL
    AuthType        NVARCHAR(50)    NOT NULL DEFAULT 'OAuth2',  -- OAuth2, APIKey, BasicAuth, SAML
    -- Encrypted credentials stored by secrets manager reference, not here
    CredentialRef   NVARCHAR(200)   NULL,        -- Key name in secrets store
    IsActive        BIT             NOT NULL DEFAULT 1,
    LastConnectedAt DATETIME2       NULL,
    ConnectionStatus NVARCHAR(50)   NULL,        -- Connected, Disconnected, Error
    ConnectionError NVARCHAR(500)   NULL,
    SyncIntervalMinutes INT         NOT NULL DEFAULT 60,
    RetryCount      INT             NOT NULL DEFAULT 3,
    TimeoutSeconds  INT             NOT NULL DEFAULT 30,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_ExternalSystems PRIMARY KEY (SystemId),
    CONSTRAINT UQ_ExternalSystems_Code UNIQUE (SystemCode),
    CONSTRAINT FK_ExternalSystems_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId)
);

-- ============================================================
-- SECTION 8: METADATA FIELD MAPPING (Sync Engine)
-- ============================================================

CREATE TABLE MetadataSyncMappings (
    MappingId       INT             NOT NULL IDENTITY(1,1),
    ExternalSystemId INT            NOT NULL,   -- FK → ExternalSystems
    WorkspaceTypeId INT             NULL,        -- NULL = applies to all workspace types for this system
    -- External side
    ExternalObjectType NVARCHAR(200) NOT NULL,  -- SAP object type
    ExternalFieldName NVARCHAR(300) NOT NULL,   -- field path in external payload
    ExternalFieldType NVARCHAR(50)  NOT NULL,   -- String, Number, Date, Boolean, Lookup
    -- Internal side
    InternalFieldId INT             NOT NULL,   -- FK → MetadataFields
    SyncDirection   NVARCHAR(20)    NOT NULL DEFAULT 'InboundOnly',  -- InboundOnly, OutboundOnly, Bidirectional
    -- Transform
    TransformExpression NVARCHAR(MAX) NULL,     -- optional: JS expression or C# template for value transform
    DefaultValue    NVARCHAR(500)   NULL,
    IsRequired      BIT             NOT NULL DEFAULT 0,
    -- Conflict resolution
    ConflictStrategy NVARCHAR(50)   NOT NULL DEFAULT 'ExternalWins',  -- ExternalWins, InternalWins, Newer, Manual
    IsActive        BIT             NOT NULL DEFAULT 1,
    SortOrder       INT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       INT             NOT NULL,
    CONSTRAINT PK_MetadataSyncMappings PRIMARY KEY (MappingId),
    CONSTRAINT FK_SyncMap_ExternalSystem FOREIGN KEY (ExternalSystemId) REFERENCES ExternalSystems(SystemId),
    CONSTRAINT FK_SyncMap_WSType FOREIGN KEY (WorkspaceTypeId) REFERENCES WorkspaceTypes(TypeId),
    CONSTRAINT FK_SyncMap_InternalField FOREIGN KEY (InternalFieldId) REFERENCES MetadataFields(FieldId),
    CONSTRAINT FK_SyncMap_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(UserId),
    CONSTRAINT CHK_SyncMap_Direction CHECK (SyncDirection IN ('InboundOnly','OutboundOnly','Bidirectional')),
    CONSTRAINT CHK_SyncMap_Conflict CHECK (ConflictStrategy IN ('ExternalWins','InternalWins','Newer','Manual'))
);
CREATE INDEX IX_SyncMap_ExternalSystem ON MetadataSyncMappings(ExternalSystemId);
CREATE INDEX IX_SyncMap_WSType         ON MetadataSyncMappings(WorkspaceTypeId);

-- ============================================================
-- SECTION 9: SYNC EVENTS LOG
-- ============================================================

CREATE TABLE SyncEventLogs (
    LogId               BIGINT          NOT NULL IDENTITY(1,1),
    ExternalSystemId    INT             NOT NULL,
    WorkspaceId         UNIQUEIDENTIFIER NULL,
    EventType           NVARCHAR(100)   NOT NULL,  -- SyncStarted, SyncCompleted, SyncFailed, ConflictDetected, FieldUpdated
    Direction           NVARCHAR(20)    NOT NULL,  -- Inbound, Outbound
    ExternalObjectId    NVARCHAR(200)   NULL,
    FieldsUpdated       NVARCHAR(MAX)   NULL,       -- JSON list of updated fields
    ConflictDetails     NVARCHAR(MAX)   NULL,       -- JSON of conflicting values
    ErrorMessage        NVARCHAR(2000)  NULL,
    DurationMs          INT             NULL,
    IsSuccessful        BIT             NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_SyncEventLogs PRIMARY KEY (LogId),
    CONSTRAINT FK_SyncLog_ExternalSystem FOREIGN KEY (ExternalSystemId) REFERENCES ExternalSystems(SystemId),
    CONSTRAINT FK_SyncLog_Workspace FOREIGN KEY (WorkspaceId) REFERENCES Workspaces(WorkspaceId)
);
CREATE INDEX IX_SyncLog_WorkspaceId     ON SyncEventLogs(WorkspaceId) WHERE WorkspaceId IS NOT NULL;
CREATE INDEX IX_SyncLog_ExternalSystem  ON SyncEventLogs(ExternalSystemId);
CREATE INDEX IX_SyncLog_CreatedAt       ON SyncEventLogs(CreatedAt DESC);
CREATE INDEX IX_SyncLog_IsSuccessful    ON SyncEventLogs(IsSuccessful) WHERE IsSuccessful = 0;

-- ============================================================
-- SECTION 10: WORKSPACE AUDIT LOG (dedicated, append-only)
-- ============================================================

CREATE TABLE WorkspaceAuditLogs (
    AuditId             BIGINT          NOT NULL IDENTITY(1,1),
    WorkspaceId         UNIQUEIDENTIFIER NOT NULL,
    EventType           NVARCHAR(100)   NOT NULL,
    UserId              INT             NULL,
    Username            NVARCHAR(100)   NULL,
    IPAddress           NVARCHAR(45)    NULL,
    OldValues           NVARCHAR(MAX)   NULL,
    NewValues           NVARCHAR(MAX)   NULL,
    AdditionalInfo      NVARCHAR(MAX)   NULL,
    Severity            NVARCHAR(20)    NOT NULL DEFAULT 'Info',
    IsSuccessful        BIT             NOT NULL DEFAULT 1,
    FailureReason       NVARCHAR(500)   NULL,
    CreatedAt           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_WorkspaceAuditLogs PRIMARY KEY (AuditId)
    -- No FK on WorkspaceId to allow logging of delete events
);
CREATE INDEX IX_WSAudit_WorkspaceId ON WorkspaceAuditLogs(WorkspaceId);
CREATE INDEX IX_WSAudit_CreatedAt   ON WorkspaceAuditLogs(CreatedAt DESC);
CREATE INDEX IX_WSAudit_EventType   ON WorkspaceAuditLogs(EventType);

-- ============================================================
-- SECTION 11: LOOKUP DATA — WORKSPACE STATUSES
-- ============================================================

-- Add workspace status category
INSERT INTO LookupCategories (Code, NameAr, NameEn, IsSystem) VALUES
('WS_STATUS', 'حالة مساحة العمل', 'Workspace Status', 1);

DECLARE @WsCatId INT = SCOPE_IDENTITY();
INSERT INTO LookupValues (CategoryId, Code, ValueAr, ValueEn, SortOrder) VALUES
(@WsCatId, 'WS_ACTIVE',    'نشطة',           'Active',          1),
(@WsCatId, 'WS_DRAFT',     'مسودة',          'Draft',           2),
(@WsCatId, 'WS_PENDING',   'في الانتظار',    'Pending',         3),
(@WsCatId, 'WS_CLOSED',    'مغلقة',          'Closed',          4),
(@WsCatId, 'WS_ARCHIVED',  'مؤرشفة',         'Archived',        5),
(@WsCatId, 'WS_DISPOSED',  'تم الإتلاف',    'Disposed',        6),
(@WsCatId, 'WS_ONHOLD',    'متوقفة',         'On Hold',         7);

-- ============================================================
-- SECTION 12: SEED — WORKSPACE TYPES
-- ============================================================

-- Seed data requires a system user (UserId=1 assumed to be SystemAdmin)
DECLARE @SysUserId INT = 1;

INSERT INTO WorkspaceTypes (Code, NameAr, NameEn, AutoCreateOnExternal, InheritRetention, InheritSecurity, DefaultExternalSystem, ExternalObjectType, IsSystem, CreatedBy)
VALUES
('PROJECT',    'مشروع',          'Project',          1, 1, 1, 'SAP_PROD', 'WBSElement',   1, @SysUserId),
('CONTRACT',   'عقد',            'Contract',         0, 1, 1, NULL,       NULL,           1, @SysUserId),
('CASE',       'قضية / ملف',    'Case',             0, 1, 1, NULL,       NULL,           1, @SysUserId),
('CUSTOMER',   'عميل / جهة',    'Customer / Entity', 1, 1, 1, 'SF_CRM',  'Account',      1, @SysUserId),
('EMPLOYEE',   'موظف',          'Employee',          1, 1, 1, 'HR_ORACLE','Employee',     1, @SysUserId),
('DEPARTMENT', 'إدارة',         'Department',        0, 1, 1, NULL,       NULL,           1, @SysUserId),
('GENERAL',    'عام',           'General',           0, 1, 0, NULL,       NULL,           1, @SysUserId);

-- ============================================================
-- SECTION 13: MODIFY DOCUMENTS TABLE (add workspace awareness)
-- ============================================================
-- Add optional WorkspaceId to existing Documents table
ALTER TABLE Documents ADD
    PrimaryWorkspaceId UNIQUEIDENTIFIER NULL;

ALTER TABLE Documents ADD
    CONSTRAINT FK_Documents_Workspace FOREIGN KEY (PrimaryWorkspaceId) REFERENCES Workspaces(WorkspaceId);

CREATE INDEX IX_Documents_WorkspaceId ON Documents(PrimaryWorkspaceId) WHERE PrimaryWorkspaceId IS NOT NULL;

-- ============================================================
-- SECTION 14: VIEWS
-- ============================================================

CREATE VIEW vw_WorkspaceSummary AS
SELECT
    w.WorkspaceId,
    w.WorkspaceNumber,
    w.TitleAr,
    w.TitleEn,
    wt.Code   AS TypeCode,
    wt.NameAr AS TypeNameAr,
    wt.NameEn AS TypeNameEn,
    lv.ValueAr AS StatusAr,
    lv.ValueEn AS StatusEn,
    cl.NameAr  AS ClassificationAr,
    w.ExternalSystemId,
    w.ExternalObjectId,
    w.ExternalObjectType,
    w.LastSyncedAt,
    w.SyncStatus,
    w.IsLegalHold,
    w.RetentionExpiresAt,
    u.FullNameAr AS OwnerNameAr,
    u.FullNameEn AS OwnerNameEn,
    d.NameAr     AS DepartmentAr,
    -- Document counts (denormalized for performance)
    (SELECT COUNT(*) FROM WorkspaceDocuments wd WHERE wd.WorkspaceId = w.WorkspaceId AND wd.IsActive = 1) AS DocumentCount,
    w.CreatedAt,
    w.UpdatedAt,
    w.IsDeleted
FROM Workspaces w
INNER JOIN WorkspaceTypes wt  ON w.WorkspaceTypeId = wt.TypeId
INNER JOIN LookupValues lv    ON w.StatusValueId   = lv.ValueId
INNER JOIN ClassificationLevels cl ON w.ClassificationLevelId = cl.LevelId
INNER JOIN Users u            ON w.OwnerId         = u.UserId
LEFT  JOIN Departments d      ON w.DepartmentId    = d.DepartmentId
WHERE w.IsDeleted = 0;
GO

CREATE VIEW vw_WorkspaceDocuments AS
SELECT
    wd.WorkspaceId,
    wd.DocumentId,
    wd.BindingType,
    wd.AddedAt,
    ds.DocumentNumber,
    ds.TitleAr,
    ds.TitleEn,
    ds.DocumentTypeAr,
    ds.StatusAr,
    ds.CurrentVersion,
    ds.FileSizeBytes,
    ds.FileExtension,
    ds.CreatedAt AS DocumentCreatedAt,
    ds.CreatedByAr
FROM WorkspaceDocuments wd
INNER JOIN vw_DocumentSummary ds ON wd.DocumentId = ds.DocumentId
WHERE wd.IsActive = 1 AND ds.IsDeleted = 0;
GO

PRINT 'xECM Extension Schema applied successfully.';
GO
