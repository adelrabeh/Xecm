namespace Darah.ECM.Domain.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
// DARAH ECM — Record-Based, Metadata-Driven Architecture
// Evolution Strategy: Layer on top of existing Document model
// ═══════════════════════════════════════════════════════════════════════════════

// ─── 1. DOMAIN (Top-level classification) ────────────────────────────────────
public sealed class RecordDomain : Common.BaseEntity
{
    public int     DomainId    { get; private set; }
    public string  Code        { get; private set; } = "";
    public string  NameAr      { get; private set; } = "";
    public string  NameEn      { get; private set; } = "";
    public string? Icon        { get; private set; }
    public string? Color       { get; private set; }
    public string? Description { get; private set; }
    public bool    IsActive    { get; private set; } = true;
    public int     SortOrder   { get; private set; }

    private RecordDomain() { }
    public static RecordDomain Create(string code, string nameAr, string nameEn,
        int createdBy, string? icon = null, string? color = null)
    {
        var d = new RecordDomain { Code=code, NameAr=nameAr, NameEn=nameEn, Icon=icon, Color=color };
        d.SetCreated(createdBy);
        return d;
    }
}

// ─── 2. RECORD TYPE (Dynamic form template) ──────────────────────────────────
public sealed class RecordType : Common.BaseEntity
{
    public int     TypeId       { get; private set; }
    public int     DomainId     { get; private set; }
    public string  Code         { get; private set; } = "";
    public string  NameAr       { get; private set; } = "";
    public string  NameEn       { get; private set; } = "";
    public string? Icon         { get; private set; }
    public string? WorkflowCode { get; private set; }  // Which workflow template to use
    public bool    RequiresApproval { get; private set; }
    public string? DefaultRetention { get; private set; }
    public bool    IsActive     { get; private set; } = true;

    private RecordType() { }
    public static RecordType Create(string code, string nameAr, string nameEn,
        int domainId, int createdBy, string? icon = null, string? workflowCode = null)
    {
        var t = new RecordType { Code=code, NameAr=nameAr, NameEn=nameEn,
            DomainId=domainId, Icon=icon, WorkflowCode=workflowCode };
        t.SetCreated(createdBy);
        return t;
    }
}

// ─── 3. METADATA FIELD DEFINITION (Dynamic form schema) ──────────────────────
public enum FieldDataType { Text, Number, Date, Select, MultiSelect, Boolean, TextArea, Email, URL, Currency }
public enum FieldScope { Core, DomainSpecific, TypeSpecific }

public sealed class MetadataFieldDef : Common.BaseEntity
{
    public int           FieldDefId   { get; private set; }
    public string        FieldKey     { get; private set; } = "";  // system key
    public string        LabelAr      { get; private set; } = "";
    public string        LabelEn      { get; private set; } = "";
    public FieldDataType DataType     { get; private set; }
    public FieldScope    Scope        { get; private set; }
    public int?          DomainId     { get; private set; }  // null = core field
    public int?          TypeId       { get; private set; }  // null = all types in domain
    public bool          IsRequired   { get; private set; }
    public bool          IsSearchable { get; private set; } = true;
    public bool          IsFacet      { get; private set; }  // Show in search facets
    public string?       Options      { get; private set; }  // JSON for Select types
    public string?       Validation   { get; private set; }  // JSON validation rules
    public string?       Placeholder  { get; private set; }
    public int           SortOrder    { get; private set; }
    public bool          IsActive     { get; private set; } = true;

    private MetadataFieldDef() { }
    public static MetadataFieldDef Create(string key, string labelAr, string labelEn,
        FieldDataType type, FieldScope scope, int createdBy,
        int? domainId = null, int? typeId = null, bool required = false)
    {
        var f = new MetadataFieldDef
        {
            FieldKey=key, LabelAr=labelAr, LabelEn=labelEn,
            DataType=type, Scope=scope, DomainId=domainId,
            TypeId=typeId, IsRequired=required
        };
        f.SetCreated(createdBy);
        return f;
    }
}

// ─── 4. RECORD (Primary entity — wraps Document) ─────────────────────────────
public enum RecordStatus { Draft, UnderReview, Approved, Active, Archived, Rejected, Cancelled }
public enum SecurityLevel  { Public, Internal, Confidential, Restricted }

public sealed class Record : Common.BaseEntity
{
    // Identity
    public long          RecordId      { get; private set; }
    public string        RecordNumber  { get; private set; } = "";  // Auto: DARAH-LEG-2026-0001
    public string        TraceId       { get; private set; } = Guid.NewGuid().ToString("N");

    // Classification (replaces folder)
    public int           DomainId      { get; private set; }
    public int           TypeId        { get; private set; }

    // Core metadata
    public string        TitleAr       { get; private set; } = "";
    public string?       TitleEn       { get; private set; }
    public string?       Description   { get; private set; }
    public string?       Department    { get; private set; }
    public string?       OwnerName     { get; private set; }
    public DateTime?     DocumentDate  { get; private set; }
    public RecordStatus  Status        { get; private set; } = RecordStatus.Draft;
    public SecurityLevel SecurityLevel { get; private set; } = SecurityLevel.Internal;
    public string?       Tags          { get; private set; }

    // Metadata values (JSON bag — flexible)
    public string?       MetadataJson  { get; private set; }

    // Link to existing Document (backward compat)
    public Guid?         DocumentId    { get; private set; }

    // Workflow
    public int?          CurrentWorkflowInstanceId { get; private set; }
    public int?          AssignedTo    { get; private set; }

    private Record() { }

    public static Record Create(string titleAr, int domainId, int typeId,
        int createdBy, string? titleEn = null, string? department = null)
    {
        var r = new Record
        {
            TitleAr=titleAr, TitleEn=titleEn, DomainId=domainId,
            TypeId=typeId, Department=department,
            RecordNumber = GenerateNumber(domainId),
            TraceId = Guid.NewGuid().ToString("N"),
            Status = RecordStatus.Draft,
        };
        r.SetCreated(createdBy);
        return r;
    }

    private static string GenerateNumber(int domainId)
    {
        var prefix = domainId switch { 1=>"LEG", 2=>"FIN", 3=>"ADM", 4=>"HIS", 5=>"RES", _=>"REC" };
        return $"DARAH-{prefix}-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(10000,99999)}";
    }

    public void UpdateStatus(RecordStatus status, int userId)
    {
        Status = status; SetUpdated(userId);
    }

    public void SetMetadata(string json, int userId)
    {
        MetadataJson = json; SetUpdated(userId);
    }

    public void LinkDocument(Guid documentId) => DocumentId = documentId;
}

// ─── 5. RECORD ATTACHMENT ─────────────────────────────────────────────────────
public sealed class RecordAttachment : Common.BaseEntity
{
    public long   AttachmentId  { get; private set; }
    public long   RecordId      { get; private set; }
    public string FileName      { get; private set; } = "";
    public string FileType      { get; private set; } = "";
    public long?  FileSizeBytes { get; private set; }
    public string StoragePath   { get; private set; } = "";
    public int    Version       { get; private set; } = 1;
    public bool   IsPrimary     { get; private set; }
    public string? Description  { get; private set; }

    private RecordAttachment() { }
    public static RecordAttachment Create(long recordId, string filename,
        string fileType, string storagePath, int createdBy, bool isPrimary = false)
    {
        var a = new RecordAttachment
        {
            RecordId=recordId, FileName=filename, FileType=fileType,
            StoragePath=storagePath, IsPrimary=isPrimary
        };
        a.SetCreated(createdBy);
        return a;
    }
}
