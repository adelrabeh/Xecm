using Darah.ECM.Domain.Common;

namespace Darah.ECM.xECM.Domain.Entities;

// ─── WORKSPACE TYPE ───────────────────────────────────────────────────────────
/// <summary>
/// Defines the business category of a workspace (Project, Contract, Case, etc.).
/// Each type can have its own metadata schema, workflow defaults, and governance rules.
/// </summary>
public sealed class WorkspaceType : BaseEntity
{
    public int    TypeId             { get; private set; }
    public string TypeCode           { get; private set; } = string.Empty;
    public string NameAr             { get; private set; } = string.Empty;
    public string NameEn             { get; private set; } = string.Empty;
    public string? Description       { get; private set; }
    public string? IconCode          { get; private set; }
    public string? Color             { get; private set; }  // HEX for UI
    public int?   DefaultRetentionId { get; private set; }
    public int?   DefaultWorkflowId  { get; private set; }
    public string? NumberPrefix      { get; private set; }  // e.g. "WS-PROJ-"
    public string? ExternalSystemCode { get; private set; } // SAP_PROD, SF_CRM, etc.
    public string? ExternalObjectType { get; private set; } // WBSElement, Account, etc.
    public bool   AllowExternalBinding { get; private set; } = true;
    public bool   RequireExternalBinding { get; private set; } = false;
    public bool   IsActive           { get; private set; } = true;
    public int    SortOrder          { get; private set; }

    private WorkspaceType() { }

    public static WorkspaceType Create(string typeCode, string nameAr, string nameEn,
        int createdBy, string? numberPrefix = null, bool allowExternal = true)
    {
        var wt = new WorkspaceType
        {
            TypeCode              = typeCode.Trim().ToUpperInvariant(),
            NameAr                = nameAr,
            NameEn                = nameEn,
            NumberPrefix          = numberPrefix,
            AllowExternalBinding  = allowExternal
        };
        wt.SetCreated(createdBy);
        return wt;
    }

    public void LinkToExternalSystem(string systemCode, string objectType, bool required)
    {
        ExternalSystemCode    = systemCode;
        ExternalObjectType    = objectType;
        RequireExternalBinding = required;
    }
}

// ─── WORKSPACE DOCUMENT BINDING ───────────────────────────────────────────────
/// <summary>
/// Links a document to a workspace. Documents can be in multiple workspaces,
/// but each has ONE primary workspace that governs its lifecycle.
///
/// BindingType:
///   Primary   — workspace drives this document's governance
///   Reference — document referenced for context, not governed
///   Output    — document is produced by this workspace (e.g., deliverable)
/// </summary>
public sealed class WorkspaceDocument : BaseEntity
{
    public int    BindingId      { get; private set; }
    public Guid   WorkspaceId   { get; private set; }
    public Guid   DocumentId    { get; private set; }
    public string BindingType   { get; private set; } = "Primary";   // Primary|Reference|Output
    public string? Note          { get; private set; }
    public bool   IsActive      { get; private set; } = true;

    private WorkspaceDocument() { }

    public static WorkspaceDocument Create(Guid workspaceId, Guid documentId,
        int addedBy, string bindingType = "Primary", string? note = null)
    {
        var b = new WorkspaceDocument
        {
            WorkspaceId = workspaceId,
            DocumentId  = documentId,
            BindingType = bindingType,
            Note        = note
        };
        b.SetCreated(addedBy);
        return b;
    }

    public void Remove(int removedBy)
    {
        IsActive = false;
        SetUpdated(removedBy);
    }
}

// ─── WORKSPACE SECURITY POLICY ────────────────────────────────────────────────
/// <summary>
/// Access control entry for a workspace.
/// Policies cascade to all bound documents with BindingType=Primary.
///
/// PrincipalType: User | Role | Department | Group
/// IsDeny: explicit deny overrides any allow at document level (deny-wins)
/// </summary>
public sealed class WorkspaceSecurityPolicy : BaseEntity
{
    public int    PolicyId       { get; set; }
    public Guid   WorkspaceId   { get; set; }
    public string PrincipalType { get; set; } = string.Empty;
    public int    PrincipalId   { get; set; }
    public bool   CanRead       { get; set; }
    public bool   CanWrite      { get; set; }
    public bool   CanDelete     { get; set; }
    public bool   CanDownload   { get; set; }
    public bool   CanManage     { get; set; }
    public bool   IsDeny        { get; set; }
    public bool   CascadeToDocuments { get; set; } = true;
    public DateTime? ExpiresAt  { get; set; }
    public int    GrantedBy     { get; set; }
    public string? Notes        { get; set; }

    private WorkspaceSecurityPolicy() { }

    public static WorkspaceSecurityPolicy Create(Guid workspaceId, string principalType,
        int principalId, bool canRead, bool canWrite, bool canDownload,
        bool canDelete, bool canManage, int grantedBy, bool isDeny = false,
        bool cascade = true, DateTime? expiresAt = null) => new()
    {
        WorkspaceId         = workspaceId,
        PrincipalType       = principalType,
        PrincipalId         = principalId,
        CanRead             = canRead,
        CanWrite            = canWrite,
        CanDownload         = canDownload,
        CanDelete           = canDelete,
        CanManage           = canManage,
        GrantedBy           = grantedBy,
        IsDeny              = isDeny,
        CascadeToDocuments  = cascade,
        ExpiresAt           = expiresAt
    };

    public bool IsExpired() => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsEffective() => !IsExpired();
}

// ─── WORKSPACE METADATA VALUE ─────────────────────────────────────────────────
/// <summary>
/// Stores a metadata field value for a workspace.
/// Same EAV pattern as DocumentMetadataValue — typed columns for query performance.
///
/// SourceType: Manual | ExternalSync | WorkflowAssigned | SystemComputed
/// </summary>
public sealed class WorkspaceMetadataValue
{
    public long    ValueId        { get; set; }
    public Guid    WorkspaceId    { get; set; }
    public int     FieldId        { get; set; }
    public string? TextValue      { get; set; }
    public decimal? NumberValue   { get; set; }
    public DateTime? DateValue    { get; set; }
    public bool?   BoolValue      { get; set; }
    public int?    LookupValueId  { get; set; }
    public string  SourceType     { get; set; } = "Manual";
    public DateTime? ExternalSyncedAt { get; set; }
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt    { get; set; }

    public void SetValue(string fieldType, string? rawValue)
    {
        TextValue = null; NumberValue = null; DateValue = null;
        BoolValue = null; LookupValueId = null; UpdatedAt = DateTime.UtcNow;
        if (rawValue is null) return;
        switch (fieldType)
        {
            case "Number":  NumberValue   = decimal.TryParse(rawValue, out var n) ? n : null; break;
            case "Date":    DateValue     = DateTime.TryParse(rawValue, out var d) ? d : null; break;
            case "Boolean": BoolValue     = bool.TryParse(rawValue, out var b) ? b : null; break;
            case "Lookup":  LookupValueId = int.TryParse(rawValue, out var l) ? l : null; break;
            default:        TextValue     = rawValue; break;
        }
    }

    public string? GetDisplayValue() =>
        TextValue ?? NumberValue?.ToString() ?? DateValue?.ToString("yyyy-MM-dd")
        ?? BoolValue?.ToString() ?? LookupValueId?.ToString();
}

// ─── WORKSPACE AUDIT LOG ──────────────────────────────────────────────────────
/// <summary>
/// Workspace-specific audit trail. Append-only.
/// Separate from the global AuditLog for workspace-centric reporting and governance.
/// </summary>
public sealed class WorkspaceAuditLog
{
    public long    LogId         { get; private set; }
    public Guid    WorkspaceId  { get; private set; }
    public string  EventType    { get; private set; } = string.Empty;
    public int?    UserId       { get; private set; }
    public string? Username     { get; private set; }
    public string? Details      { get; private set; }  // JSON
    public string? OldValues    { get; private set; }  // JSON
    public string? NewValues    { get; private set; }  // JSON
    public string? CorrelationId { get; private set; }
    public string  Severity     { get; private set; } = "Info";
    public DateTime CreatedAt   { get; private set; } = DateTime.UtcNow;

    private WorkspaceAuditLog() { }

    public static WorkspaceAuditLog Create(Guid workspaceId, string eventType,
        int? userId = null, string? username = null, string? details = null,
        string? oldValues = null, string? newValues = null,
        string? correlationId = null, string severity = "Info") => new()
    {
        WorkspaceId   = workspaceId,
        EventType     = eventType,
        UserId        = userId,
        Username      = username,
        Details       = details,
        OldValues     = oldValues,
        NewValues     = newValues,
        CorrelationId = correlationId,
        Severity      = severity,
        CreatedAt     = DateTime.UtcNow
    };
}

// ─── EXTERNAL SYSTEM REGISTRY ─────────────────────────────────────────────────
/// <summary>
/// Registry of connected external systems.
/// Configures connection details (stored securely — passwords via secrets manager, not DB).
/// </summary>
public sealed class ExternalSystem : BaseEntity
{
    public int    SystemId      { get; private set; }
    public string SystemCode    { get; private set; } = string.Empty;  // SAP_PROD, SF_CRM
    public string NameAr        { get; private set; } = string.Empty;
    public string NameEn        { get; private set; } = string.Empty;

    /// <summary>SAP|Salesforce|OracleHR|GenericREST|OpenText</summary>
    public string SystemType    { get; private set; } = string.Empty;

    public string  BaseUrl      { get; private set; } = string.Empty;
    public string? AuthType     { get; private set; }  // OAuth2|Basic|ApiKey|Certificate
    public string? ClientId     { get; private set; }
    // ClientSecret stored in secrets manager, not in DB
    public string? TenantId     { get; private set; }
    public bool    IsActive     { get; private set; } = true;
    public bool    TestMode     { get; private set; } = false;
    public DateTime? LastTestedAt  { get; private set; }
    public bool?   LastTestResult  { get; private set; }
    public string? LastTestError   { get; private set; }

    private ExternalSystem() { }

    public static ExternalSystem Create(string systemCode, string nameAr, string nameEn,
        string systemType, string baseUrl, int createdBy, string? authType = null)
    {
        var es = new ExternalSystem
        {
            SystemCode = systemCode.Trim().ToUpperInvariant(),
            NameAr     = nameAr,
            NameEn     = nameEn,
            SystemType = systemType,
            BaseUrl    = baseUrl.TrimEnd('/'),
            AuthType   = authType
        };
        es.SetCreated(createdBy);
        return es;
    }

    public void RecordConnectionTest(bool success, string? error = null)
    {
        LastTestedAt  = DateTime.UtcNow;
        LastTestResult = success;
        LastTestError  = error;
    }
}

// ─── METADATA SYNC MAPPING ────────────────────────────────────────────────────
/// <summary>
/// Configurable field mapping: external system field → ECM metadata field.
/// Stored in DB — administrators can change mappings without code deployment.
/// </summary>
public sealed class MetadataSyncMapping : BaseEntity
{
    public int    MappingId           { get; private set; }
    public int    ExternalSystemId    { get; private set; }
    public string? WorkspaceTypeCode  { get; private set; }  // null = all types
    public string  ExternalObjectType { get; private set; } = string.Empty;
    public string  ExternalFieldName  { get; private set; } = string.Empty;
    public string  ExternalFieldType  { get; private set; } = string.Empty;
    public int     InternalFieldId    { get; private set; }

    /// <summary>InboundOnly|OutboundOnly|Bidirectional</summary>
    public string  SyncDirection      { get; private set; } = "InboundOnly";

    /// <summary>ExternalWins|InternalWins|Newer|Manual</summary>
    public string  ConflictStrategy   { get; private set; } = "ExternalWins";

    /// <summary>uppercase|lowercase|trim|titlecase|prefix:X|suffix:X</summary>
    public string? TransformExpression { get; private set; }
    public string? DefaultValue        { get; private set; }
    public bool    IsRequired          { get; private set; }
    public bool    IsActive            { get; private set; } = true;
    public int     SortOrder           { get; private set; }

    private MetadataSyncMapping() { }

    public static MetadataSyncMapping Create(int systemId, string objectType,
        string externalField, string externalType, int internalFieldId,
        int createdBy, string direction = "InboundOnly",
        string conflict = "ExternalWins", string? transform = null)
    {
        var m = new MetadataSyncMapping
        {
            ExternalSystemId   = systemId,
            ExternalObjectType = objectType,
            ExternalFieldName  = externalField,
            ExternalFieldType  = externalType,
            InternalFieldId    = internalFieldId,
            SyncDirection      = direction,
            ConflictStrategy   = conflict,
            TransformExpression = transform
        };
        m.SetCreated(createdBy);
        return m;
    }
}

// ─── SYNC EVENT LOG ───────────────────────────────────────────────────────────
/// <summary>
/// Immutable record of every sync operation for a workspace.
/// Enables: audit, retry analysis, conflict tracking, SLA reporting.
/// </summary>
public sealed class SyncEventLog
{
    public long   LogId            { get; private set; }
    public Guid   WorkspaceId      { get; private set; }
    public int    ExternalSystemId { get; private set; }

    /// <summary>Manual|Scheduled|Webhook</summary>
    public string TriggerType      { get; private set; } = string.Empty;

    /// <summary>Inbound|Outbound|Bidirectional</summary>
    public string SyncDirection    { get; private set; } = string.Empty;

    public string? ExternalObjectId  { get; private set; }
    public string? ExternalObjectType { get; private set; }
    public bool    IsSuccessful     { get; private set; }
    public int     FieldsUpdated    { get; private set; }
    public int     ConflictsDetected { get; private set; }
    public string? ErrorMessage     { get; private set; }
    public long    DurationMs       { get; private set; }
    public DateTime CreatedAt       { get; private set; } = DateTime.UtcNow;

    private SyncEventLog() { }

    public static SyncEventLog CreateSuccess(Guid workspaceId, int systemId,
        string direction, string trigger, int fieldsUpdated,
        int conflicts, long durationMs, string? objectId, string? objectType) => new()
    {
        WorkspaceId        = workspaceId,
        ExternalSystemId   = systemId,
        SyncDirection      = direction,
        TriggerType        = trigger,
        ExternalObjectId   = objectId,
        ExternalObjectType = objectType,
        IsSuccessful       = true,
        FieldsUpdated      = fieldsUpdated,
        ConflictsDetected  = conflicts,
        DurationMs         = durationMs,
        CreatedAt          = DateTime.UtcNow
    };

    public static SyncEventLog CreateFailure(Guid workspaceId, int systemId,
        string direction, string trigger, string error, long durationMs) => new()
    {
        WorkspaceId      = workspaceId,
        ExternalSystemId = systemId,
        SyncDirection    = direction,
        TriggerType      = trigger,
        IsSuccessful     = false,
        ErrorMessage     = error,
        DurationMs       = durationMs,
        CreatedAt        = DateTime.UtcNow
    };
}
