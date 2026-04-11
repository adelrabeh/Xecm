using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Events;
using Darah.ECM.Domain.ValueObjects;

namespace Darah.ECM.Domain.Entities;

// ─────────────────────────────────────────────────────────────
// USER
// ─────────────────────────────────────────────────────────────
public class User : BaseEntity
{
    public int UserId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullNameAr { get; private set; } = string.Empty;
    public string? FullNameEn { get; private set; }
    public int? DepartmentId { get; private set; }
    public string? JobTitle { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsLocked { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? LastLoginIP { get; private set; }
    public bool MFAEnabled { get; private set; }
    public string LanguagePreference { get; private set; } = "ar";
    public bool MustChangePassword { get; private set; }
    public string? ExternalId { get; private set; }

    private User() { }

    public static User Create(string username, string email, string passwordHash,
        string fullNameAr, int createdBy, int? departmentId = null, string? fullNameEn = null)
    {
        var user = new User
        {
            Username = username.Trim().ToLowerInvariant(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullNameAr = fullNameAr,
            FullNameEn = fullNameEn,
            DepartmentId = departmentId,
            IsActive = true
        };
        user.SetCreated(createdBy);
        return user;
    }

    public void RecordLogin(string ip)
    {
        LastLoginAt = DateTime.UtcNow;
        LastLoginIP = ip;
        FailedLoginAttempts = 0;
        IsLocked = false;
        LockoutEnd = null;
        SetUpdated(UserId);
    }

    public void IncrementFailedLogin() { FailedLoginAttempts++; SetUpdated(UserId); }

    public void Lock(int durationMinutes)
    {
        IsLocked = true;
        LockoutEnd = DateTime.UtcNow.AddMinutes(durationMinutes);
        SetUpdated(UserId);
    }

    public void Unlock() { IsLocked = false; LockoutEnd = null; FailedLoginAttempts = 0; SetUpdated(UserId); }

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash;
        MustChangePassword = false;
        SetUpdated(UserId);
    }

    public bool IsLockedOut() => IsLocked && LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;
}

// ─────────────────────────────────────────────────────────────
// DOCUMENT
// ─────────────────────────────────────────────────────────────
public class Document : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public string TitleAr { get; private set; } = string.Empty;
    public string? TitleEn { get; private set; }
    public int DocumentTypeId { get; private set; }
    public int LibraryId { get; private set; }
    public int? FolderId { get; private set; }
    public int? RecordClassId { get; private set; }
    public int? RetentionPolicyId { get; private set; }
    public int? CurrentVersionId { get; private set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;
    public ClassificationLevel Classification { get; private set; } = ClassificationLevel.Internal;
    public int? CheckedOutBy { get; private set; }
    public DateTime? CheckedOutAt { get; private set; }
    public bool IsCheckedOut { get; private set; }
    public bool IsLegalHold { get; private set; }
    public DateOnly? RetentionExpiresAt { get; private set; }
    public string? Keywords { get; private set; }
    public string? Summary { get; private set; }
    public DateOnly? DocumentDate { get; private set; }
    public Guid? PrimaryWorkspaceId { get; private set; }

    private Document() { }

    public static Document Create(string titleAr, int documentTypeId, int libraryId,
        int createdBy, string documentNumber, string? titleEn = null,
        int? folderId = null, ClassificationLevel? classification = null,
        DateOnly? documentDate = null, string? keywords = null, string? summary = null)
    {
        var doc = new Document
        {
            DocumentId = Guid.NewGuid(),
            DocumentNumber = documentNumber,
            TitleAr = titleAr,
            TitleEn = titleEn,
            DocumentTypeId = documentTypeId,
            LibraryId = libraryId,
            FolderId = folderId,
            Classification = classification ?? ClassificationLevel.Internal,
            Status = DocumentStatus.Draft,
            DocumentDate = documentDate,
            Keywords = keywords,
            Summary = summary
        };
        doc.SetCreated(createdBy);
        doc.RaiseDomainEvent(new DocumentCreatedEvent(doc.DocumentId, doc.DocumentNumber, doc.TitleAr, createdBy));
        return doc;
    }

    public void CheckOut(int userId)
    {
        if (IsCheckedOut) throw new InvalidOperationException("Document is already checked out.");
        if (IsLegalHold) throw new InvalidOperationException("Cannot check out a document under legal hold.");
        IsCheckedOut = true;
        CheckedOutBy = userId;
        CheckedOutAt = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void CheckIn(int versionId, int userId)
    {
        IsCheckedOut = false;
        CheckedOutBy = null;
        CheckedOutAt = null;
        CurrentVersionId = versionId;
        SetUpdated(userId);
    }

    public void TransitionStatus(DocumentStatus newStatus, int userId)
    {
        if (!Status.CanTransitionTo(newStatus))
            throw new InvalidOperationException($"Cannot transition from '{Status}' to '{newStatus}'.");
        var previous = Status;
        Status = newStatus;
        SetUpdated(userId);
        if (newStatus == DocumentStatus.Approved)
            RaiseDomainEvent(new DocumentApprovedEvent(DocumentId, DocumentNumber, userId));
        if (newStatus == DocumentStatus.Archived)
            RaiseDomainEvent(new DocumentArchivedEvent(DocumentId, userId));
    }

    public void UpdateClassification(ClassificationLevel level, int userId)
    {
        Classification = level;
        SetUpdated(userId);
    }

    public void ApplyLegalHold() => IsLegalHold = true;
    public void ReleaseLegalHold() => IsLegalHold = false;

    public void SetRetentionExpiry(DateOnly expiry, int userId)
    {
        RetentionExpiresAt = expiry;
        SetUpdated(userId);
    }

    public void SetPrimaryWorkspace(Guid workspaceId, int userId)
    {
        PrimaryWorkspaceId = workspaceId;
        SetUpdated(userId);
    }

    public void UpdateContent(string titleAr, string? titleEn, string? keywords, string? summary,
        DateOnly? documentDate, int userId)
    {
        TitleAr = titleAr;
        TitleEn = titleEn;
        Keywords = keywords;
        Summary = summary;
        DocumentDate = documentDate;
        SetUpdated(userId);
    }
}

// ─────────────────────────────────────────────────────────────
// DOCUMENT VERSION
// ─────────────────────────────────────────────────────────────
public class DocumentVersion : BaseEntity
{
    public int VersionId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string VersionNumber { get; private set; } = string.Empty;
    public int MajorVersion { get; private set; }
    public int MinorVersion { get; private set; }
    public FileMetadata File { get; private set; } = null!;
    public string? ChangeNote { get; private set; }
    public bool IsCurrent { get; private set; } = true;

    private DocumentVersion() { }

    public static DocumentVersion Create(Guid documentId, string versionNumber, int major, int minor,
        FileMetadata file, int createdBy, string? changeNote = null)
    {
        var v = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = versionNumber,
            MajorVersion = major,
            MinorVersion = minor,
            File = file,
            ChangeNote = changeNote,
            IsCurrent = true
        };
        v.SetCreated(createdBy);
        return v;
    }

    public void MarkSuperseded() => IsCurrent = false;
}

// ─────────────────────────────────────────────────────────────
// WORKFLOW INSTANCE
// ─────────────────────────────────────────────────────────────
public class WorkflowInstance : BaseEntity
{
    public int InstanceId { get; private set; }
    public int DefinitionId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Status { get; private set; } = "InProgress";
    public int? CurrentStepId { get; private set; }
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public int StartedBy { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int Priority { get; private set; } = 2;

    private readonly List<WorkflowTask> _tasks = new();
    public IReadOnlyCollection<WorkflowTask> Tasks => _tasks.AsReadOnly();

    private WorkflowInstance() { }

    public static WorkflowInstance Start(int definitionId, Guid documentId, int startedBy, int priority = 2)
    {
        var inst = new WorkflowInstance
        {
            DefinitionId = definitionId,
            DocumentId = documentId,
            StartedBy = startedBy,
            Priority = priority,
            StartedAt = DateTime.UtcNow,
            Status = "InProgress"
        };
        inst.SetCreated(startedBy);
        return inst;
    }

    public void MoveToStep(int stepId) => CurrentStepId = stepId;

    public void Complete()
    {
        Status = "Approved";
        CompletedAt = DateTime.UtcNow;
        RaiseDomainEvent(new WorkflowCompletedEvent(InstanceId, DocumentId, StartedBy));
    }

    public void Reject() { Status = "Rejected"; CompletedAt = DateTime.UtcNow; }
    public void Cancel(string reason) { Status = "Cancelled"; CompletedAt = DateTime.UtcNow; }
}

// ─────────────────────────────────────────────────────────────
// WORKFLOW TASK
// ─────────────────────────────────────────────────────────────
public class WorkflowTask : BaseEntity
{
    public int TaskId { get; private set; }
    public int InstanceId { get; private set; }
    public int StepId { get; private set; }
    public int? AssignedToUserId { get; private set; }
    public int? AssignedToRoleId { get; private set; }
    public string Status { get; private set; } = "Pending";
    public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int? CompletedBy { get; private set; }
    public bool IsOverdue { get; private set; }
    public bool IsDelegated { get; private set; }
    public DateTime? EscalatedAt { get; private set; }

    private WorkflowTask() { }

    public static WorkflowTask Create(int instanceId, int stepId,
        int? userId, int? roleId, int? slaHours)
    {
        var t = new WorkflowTask
        {
            InstanceId = instanceId,
            StepId = stepId,
            AssignedToUserId = userId,
            AssignedToRoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            Status = "Pending"
        };
        if (slaHours.HasValue) t.DueAt = DateTime.UtcNow.AddHours(slaHours.Value);
        return t;
    }

    public void Complete(int userId) { Status = "Completed"; CompletedAt = DateTime.UtcNow; CompletedBy = userId; }
    public void MarkOverdue() => IsOverdue = true;
    public void MarkEscalated() => EscalatedAt = DateTime.UtcNow;
    public void Delegate(int newUserId) { AssignedToUserId = newUserId; IsDelegated = true; }
}

// ─────────────────────────────────────────────────────────────
// AUDIT LOG (append-only)
// ─────────────────────────────────────────────────────────────
public class AuditLog
{
    public long AuditId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string? EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public int? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? SessionId { get; private set; }
    public string? IPAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? AdditionalInfo { get; private set; }
    public string Severity { get; private set; } = "Info";
    public bool IsSuccessful { get; private set; } = true;
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AuditLog() { }

    public static AuditLog Create(string eventType, string? entityType = null, string? entityId = null,
        int? userId = null, string? username = null, string? ipAddress = null,
        string? oldValues = null, string? newValues = null, string? additionalInfo = null,
        string severity = "Info", bool isSuccessful = true, string? failureReason = null,
        string? sessionId = null, string? userAgent = null) => new()
    {
        EventType = eventType, EntityType = entityType, EntityId = entityId,
        UserId = userId, Username = username, SessionId = sessionId,
        IPAddress = ipAddress, UserAgent = userAgent,
        OldValues = oldValues, NewValues = newValues, AdditionalInfo = additionalInfo,
        Severity = severity, IsSuccessful = isSuccessful, FailureReason = failureReason,
        CreatedAt = DateTime.UtcNow
    };
}
