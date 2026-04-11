using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Events.Records;

namespace Darah.ECM.Domain.Entities;

// ─── RECORD CLASS ─────────────────────────────────────────────────────────────
/// <summary>
/// Classification hierarchy for records (e.g., Finance > Contracts > Procurement).
/// Supports unlimited nesting via ParentId.
/// </summary>
public sealed class RecordClass : BaseEntity
{
    public int     ClassId         { get; private set; }
    public int?    ParentId        { get; private set; }
    public string  Code            { get; private set; } = string.Empty;
    public string  NameAr          { get; private set; } = string.Empty;
    public string? NameEn          { get; private set; }
    public string? Description     { get; private set; }
    public int?    RetentionYears  { get; private set; }

    /// <summary>Delete|Archive|Transfer|Review</summary>
    public string  DisposalAction  { get; private set; } = "Review";
    public bool    IsActive        { get; private set; } = true;

    private RecordClass() { }

    public static RecordClass Create(string code, string nameAr, int createdBy,
        string? nameEn = null, int? parentId = null,
        int? retentionYears = null, string disposalAction = "Review")
    {
        var rc = new RecordClass
        {
            Code           = code.Trim().ToUpperInvariant(),
            NameAr         = nameAr,
            NameEn         = nameEn,
            ParentId       = parentId,
            RetentionYears = retentionYears,
            DisposalAction = disposalAction
        };
        rc.SetCreated(createdBy);
        return rc;
    }
}

// ─── RETENTION POLICY ─────────────────────────────────────────────────────────
/// <summary>
/// Defines how long documents are kept and what happens at expiry.
/// Applied at document type, record class, or workspace level.
/// </summary>
public sealed class RetentionPolicy : BaseEntity
{
    public int     PolicyId         { get; private set; }
    public string  Code             { get; private set; } = string.Empty;
    public string  NameAr           { get; private set; } = string.Empty;
    public string? NameEn           { get; private set; }
    public string? Description      { get; private set; }
    public int     RetentionYears   { get; private set; }

    /// <summary>CreationDate|DocumentDate|LastModified|EventBased</summary>
    public string  RetentionTrigger { get; private set; } = "CreationDate";

    /// <summary>Delete|Archive|Transfer|Review</summary>
    public string  DisposalAction   { get; private set; } = "Review";
    public bool    RequiresReview   { get; private set; } = true;
    public string? LegalReference   { get; private set; }
    public bool    IsActive         { get; private set; } = true;

    private RetentionPolicy() { }

    public static RetentionPolicy Create(string code, string nameAr, int years,
        int createdBy, string? nameEn = null,
        string trigger = "CreationDate", string disposal = "Review",
        bool requiresReview = true, string? legalRef = null)
    {
        if (years < 0) throw new ArgumentException("Retention years cannot be negative.");
        var policy = new RetentionPolicy
        {
            Code             = code.Trim().ToUpperInvariant(),
            NameAr           = nameAr,
            NameEn           = nameEn,
            RetentionYears   = years,
            RetentionTrigger = trigger,
            DisposalAction   = disposal,
            RequiresReview   = requiresReview,
            LegalReference   = legalRef
        };
        policy.SetCreated(createdBy);
        return policy;
    }

    public DateOnly ComputeExpiry(DateOnly triggerDate)
        => RetentionYears == 9999 ? DateOnly.MaxValue : triggerDate.AddYears(RetentionYears);
}

// ─── LEGAL HOLD ───────────────────────────────────────────────────────────────
/// <summary>
/// Legal hold — suspends retention and disposal for documents under litigation or audit.
/// Can be applied at document level or workspace level.
/// </summary>
public sealed class LegalHold : BaseEntity
{
    public int     HoldId          { get; private set; }
    public string  HoldCode        { get; private set; } = string.Empty;
    public string  NameAr          { get; private set; } = string.Empty;
    public string? NameEn          { get; private set; }
    public string  Reason          { get; private set; } = string.Empty;
    public string? CaseReference   { get; private set; }
    public DateOnly StartDate      { get; private set; }
    public DateOnly? EndDate       { get; private set; }
    public bool    IsActive        { get; private set; } = true;
    public DateTime? ReleasedAt    { get; private set; }
    public int?    ReleasedBy      { get; private set; }

    private LegalHold() { }

    public static LegalHold Create(string code, string nameAr, string reason,
        DateOnly startDate, int createdBy,
        string? nameEn = null, string? caseRef = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Legal hold reason is required.");
        var hold = new LegalHold
        {
            HoldCode      = code.Trim().ToUpperInvariant(),
            NameAr        = nameAr,
            NameEn        = nameEn,
            Reason        = reason,
            CaseReference = caseRef,
            StartDate     = startDate
        };
        hold.SetCreated(createdBy);
        return hold;
    }

    public void Release(int releasedBy, DateOnly endDate)
    {
        IsActive   = false;
        EndDate    = endDate;
        ReleasedAt = DateTime.UtcNow;
        ReleasedBy = releasedBy;
        SetUpdated(releasedBy);
    }
}

// ─── DOCUMENT LEGAL HOLD (JOIN) ───────────────────────────────────────────────
/// <summary>Many-to-many link between a document and a legal hold.</summary>
public sealed class DocumentLegalHold
{
    public int     Id          { get; set; }
    public Guid    DocumentId  { get; set; }
    public int     HoldId      { get; set; }
    public DateTime AppliedAt  { get; set; } = DateTime.UtcNow;
    public int     AppliedBy   { get; set; }
}

// ─── DISPOSAL REQUEST ─────────────────────────────────────────────────────────
/// <summary>
/// A formal request to dispose (delete, archive, or transfer) one or more documents.
/// Goes through approval workflow before execution.
/// </summary>
public sealed class DisposalRequest : BaseEntity
{
    public int     RequestId       { get; private set; }
    public string  RequestCode     { get; private set; } = string.Empty;

    /// <summary>Delete|Archive|Transfer</summary>
    public string  DisposalType    { get; private set; } = string.Empty;

    /// <summary>Pending|Approved|Rejected|Executed</summary>
    public string  Status          { get; private set; } = "Pending";
    public string  Justification   { get; private set; } = string.Empty;
    public int?    ApprovedBy      { get; private set; }
    public DateTime? ApprovedAt    { get; private set; }
    public DateTime? ExecutedAt    { get; private set; }
    public int     DocumentCount   { get; private set; }

    private DisposalRequest() { }

    public static DisposalRequest Create(string code, string type,
        string justification, int documentCount, int createdBy)
    {
        var req = new DisposalRequest
        {
            RequestCode   = code,
            DisposalType  = type,
            Justification = justification,
            Status        = "Pending",
            DocumentCount = documentCount
        };
        req.SetCreated(createdBy);
        return req;
    }

    public void Approve(int approvedBy)
    {
        Status     = "Approved";
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        SetUpdated(approvedBy);
    }

    public void Reject(int rejectedBy)
    {
        Status = "Rejected";
        SetUpdated(rejectedBy);
    }

    public void MarkExecuted(int executedBy)
    {
        Status     = "Executed";
        ExecutedAt = DateTime.UtcNow;
        SetUpdated(executedBy);
    }
}

// ─── NOTIFICATION ─────────────────────────────────────────────────────────────
/// <summary>In-app notification delivered to a specific user.</summary>
public sealed class Notification : BaseEntity
{
    public long    NotificationId   { get; private set; }
    public int     UserId           { get; private set; }
    public string  Title            { get; private set; } = string.Empty;
    public string  Body             { get; private set; } = string.Empty;
    public string  NotificationType { get; private set; } = string.Empty;
    public string? EntityType       { get; private set; }
    public string? EntityId         { get; private set; }
    public string? ActionUrl        { get; private set; }
    public int     Priority         { get; private set; } = 2;
    public bool    IsRead           { get; private set; } = false;
    public DateTime? ReadAt         { get; private set; }
    public DateTime? ExpiresAt      { get; private set; }

    private Notification() { }

    public static Notification Create(int userId, string title, string body,
        string type, string? entityType = null, string? entityId = null,
        string? actionUrl = null, int priority = 2, DateTime? expiresAt = null)
    {
        var n = new Notification
        {
            UserId           = userId,
            Title            = title,
            Body             = body,
            NotificationType = type,
            EntityType       = entityType,
            EntityId         = entityId,
            ActionUrl        = actionUrl,
            Priority         = priority,
            ExpiresAt        = expiresAt
        };
        n.SetCreated(userId);
        return n;
    }

    public void MarkRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
