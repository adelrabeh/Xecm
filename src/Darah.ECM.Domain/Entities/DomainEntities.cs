// ============================================================
// FILE: src/Domain/Common/BaseEntity.cs
// ============================================================
namespace Darah.ECM.Domain.Common;

public abstract class BaseEntity
{
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public int CreatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public int? UpdatedBy { get; protected set; }
    public bool IsDeleted { get; protected set; } = false;
    public DateTime? DeletedAt { get; protected set; }
    public int? DeletedBy { get; protected set; }

    protected BaseEntity() { }

    public void SetCreated(int userId) { CreatedBy = userId; CreatedAt = DateTime.UtcNow; }
    public void SetUpdated(int userId) { UpdatedBy = userId; UpdatedAt = DateTime.UtcNow; }
    public void SoftDelete(int userId) { IsDeleted = true; DeletedAt = DateTime.UtcNow; DeletedBy = userId; }
}

// ============================================================
// FILE: src/Domain/Enums/DocumentStatus.cs
// ============================================================
namespace Darah.ECM.Domain.Enums;

public enum DocumentStatus { Draft = 1, Active, Pending, Approved, Rejected, Archived, Superseded, Disposed }
public enum ClassificationLevel { Public = 1, Internal, Confidential, Secret }
public enum WorkflowInstanceStatus { InProgress, Approved, Rejected, Returned, Cancelled, Archived }
public enum WorkflowTaskStatus { Pending, Completed, Skipped, Expired }
public enum WorkflowActionType { Approve, Reject, Return, Delegate, Escalate, Comment }
public enum AssigneeType { SpecificUser, Role, Department, Dynamic, Sequential, Parallel }
public enum NotificationSeverity { Info, Warning, Critical }

// ============================================================
// FILE: src/Domain/Entities/User.cs
// ============================================================
namespace Darah.ECM.Domain.Entities;

public class User
{
    public int UserId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullNameAr { get; private set; } = string.Empty;
    public string? FullNameEn { get; private set; }
    public int? DepartmentId { get; private set; }
    public string? JobTitle { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsLocked { get; private set; } = false;
    public DateTime? LockoutEnd { get; private set; }
    public int FailedLoginAttempts { get; private set; } = 0;
    public DateTime? LastLoginAt { get; private set; }
    public string? LastLoginIP { get; private set; }
    public bool MFAEnabled { get; private set; } = false;
    public string? MFASecret { get; private set; }
    public string LanguagePreference { get; private set; } = "ar";
    public bool MustChangePassword { get; private set; } = false;
    public DateTime? PasswordChangedAt { get; private set; }
    public string? ExternalId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; } = false;

    // Navigation
    public virtual Department? Department { get; private set; }
    public virtual ICollection<UserRole> UserRoles { get; private set; } = new HashSet<UserRole>();

    private User() { }

    public static User Create(string username, string email, string passwordHash, string fullNameAr, int createdBy, int? departmentId = null, string? fullNameEn = null)
    {
        return new User
        {
            Username = username.Trim().ToLower(),
            Email = email.Trim().ToLower(),
            PasswordHash = passwordHash,
            FullNameAr = fullNameAr,
            FullNameEn = fullNameEn,
            DepartmentId = departmentId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordLogin(string ipAddress) { LastLoginAt = DateTime.UtcNow; LastLoginIP = ipAddress; FailedLoginAttempts = 0; IsLocked = false; LockoutEnd = null; UpdatedAt = DateTime.UtcNow; }
    public void IncrementFailedLogin() { FailedLoginAttempts++; UpdatedAt = DateTime.UtcNow; }
    public void Lock(int durationMinutes) { IsLocked = true; LockoutEnd = DateTime.UtcNow.AddMinutes(durationMinutes); UpdatedAt = DateTime.UtcNow; }
    public void Unlock() { IsLocked = false; LockoutEnd = null; FailedLoginAttempts = 0; UpdatedAt = DateTime.UtcNow; }
    public void ChangePassword(string newHash) { PasswordHash = newHash; MustChangePassword = false; PasswordChangedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public bool IsLockedOut() => IsLocked && LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;
}

// ============================================================
// FILE: src/Domain/Entities/Document.cs
// ============================================================
namespace Darah.ECM.Domain.Entities;

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
    public int StatusValueId { get; private set; }
    public int ClassificationLevelId { get; private set; } = 1;
    public int? CheckedOutBy { get; private set; }
    public DateTime? CheckedOutAt { get; private set; }
    public bool IsCheckedOut { get; private set; } = false;
    public bool IsLegalHold { get; private set; } = false;
    public DateOnly? RetentionExpiresAt { get; private set; }
    public string? Keywords { get; private set; }
    public string? Summary { get; private set; }
    public string? SourceReference { get; private set; }
    public DateOnly? DocumentDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public int? PageCount { get; private set; }
    public string Language { get; private set; } = "ar";

    // Navigation
    public virtual DocumentType DocumentType { get; private set; } = null!;
    public virtual DocumentLibrary Library { get; private set; } = null!;
    public virtual Folder? Folder { get; private set; }
    public virtual ICollection<DocumentVersion> Versions { get; private set; } = new HashSet<DocumentVersion>();
    public virtual ICollection<DocumentMetadataValue> MetadataValues { get; private set; } = new HashSet<DocumentMetadataValue>();
    public virtual ICollection<DocumentTag> Tags { get; private set; } = new HashSet<DocumentTag>();
    public virtual ICollection<DocumentComment> Comments { get; private set; } = new HashSet<DocumentComment>();

    private Document() { }

    public static Document Create(string titleAr, int documentTypeId, int libraryId, int statusValueId, int createdBy, string documentNumber, string? titleEn = null, int? folderId = null, int? retentionPolicyId = null, int classificationLevelId = 1, DateOnly? documentDate = null, string? keywords = null, string? summary = null)
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
            StatusValueId = statusValueId,
            ClassificationLevelId = classificationLevelId,
            RetentionPolicyId = retentionPolicyId,
            DocumentDate = documentDate,
            Keywords = keywords,
            Summary = summary
        };
        doc.SetCreated(createdBy);
        return doc;
    }

    public void CheckOut(int userId) { if (IsCheckedOut) throw new InvalidOperationException("Document is already checked out."); IsCheckedOut = true; CheckedOutBy = userId; CheckedOutAt = DateTime.UtcNow; SetUpdated(userId); }
    public void CheckIn(int versionId, int userId) { IsCheckedOut = false; CheckedOutBy = null; CheckedOutAt = null; CurrentVersionId = versionId; SetUpdated(userId); }
    public void ApplyLegalHold() { IsLegalHold = true; }
    public void ReleaseLegalHold() { IsLegalHold = false; }
    public void SetRetentionExpiry(DateOnly expiryDate, int userId) { RetentionExpiresAt = expiryDate; SetUpdated(userId); }
    public void UpdateStatus(int statusValueId, int userId) { StatusValueId = statusValueId; SetUpdated(userId); }
    public void UpdateTitle(string titleAr, string? titleEn, int userId) { TitleAr = titleAr; TitleEn = titleEn; SetUpdated(userId); }
    public void UpdateClassification(int levelId, int userId) { ClassificationLevelId = levelId; SetUpdated(userId); }
    public void UpdateMetadata(string? keywords, string? summary, DateOnly? documentDate, int userId) { Keywords = keywords; Summary = summary; DocumentDate = documentDate; SetUpdated(userId); }
}

// ============================================================
// FILE: src/Domain/Entities/WorkflowInstance.cs
// ============================================================
namespace Darah.ECM.Domain.Entities;

public class WorkflowInstance
{
    public int InstanceId { get; private set; }
    public int DefinitionId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Status { get; private set; } = "InProgress";
    public int? CurrentStepId { get; private set; }
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public int StartedBy { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public int? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public int Priority { get; private set; } = 2;
    public DateOnly? DueDate { get; private set; }

    public virtual WorkflowDefinition Definition { get; private set; } = null!;
    public virtual Document Document { get; private set; } = null!;
    public virtual ICollection<WorkflowTask> Tasks { get; private set; } = new HashSet<WorkflowTask>();

    private WorkflowInstance() { }

    public static WorkflowInstance Start(int definitionId, Guid documentId, int startedBy, int priority = 2)
    {
        return new WorkflowInstance { DefinitionId = definitionId, DocumentId = documentId, StartedBy = startedBy, Priority = priority, StartedAt = DateTime.UtcNow, Status = "InProgress" };
    }

    public void MoveToStep(int stepId) { CurrentStepId = stepId; }
    public void Complete() { Status = "Approved"; CompletedAt = DateTime.UtcNow; }
    public void Reject() { Status = "Rejected"; CompletedAt = DateTime.UtcNow; }
    public void Cancel(int cancelledBy, string reason) { Status = "Cancelled"; CancelledAt = DateTime.UtcNow; CancelledBy = cancelledBy; CancellationReason = reason; }
}

// ============================================================
// FILE: src/Domain/Entities/WorkflowTask.cs
// ============================================================
namespace Darah.ECM.Domain.Entities;

public class WorkflowTask
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
    public bool IsOverdue { get; private set; } = false;
    public bool IsDelegated { get; private set; } = false;
    public int? DelegatedFrom { get; private set; }
    public DateTime? SLABreachNotifiedAt { get; private set; }
    public DateTime? EscalatedAt { get; private set; }

    public virtual WorkflowInstance Instance { get; private set; } = null!;
    public virtual WorkflowStep Step { get; private set; } = null!;
    public virtual ICollection<WorkflowAction> Actions { get; private set; } = new HashSet<WorkflowAction>();

    private WorkflowTask() { }

    public static WorkflowTask Create(int instanceId, int stepId, int? userId, int? roleId, int? slaHours)
    {
        var task = new WorkflowTask { InstanceId = instanceId, StepId = stepId, AssignedToUserId = userId, AssignedToRoleId = roleId, AssignedAt = DateTime.UtcNow, Status = "Pending" };
        if (slaHours.HasValue) task.DueAt = DateTime.UtcNow.AddHours(slaHours.Value);
        return task;
    }

    public void Complete(int userId) { Status = "Completed"; CompletedAt = DateTime.UtcNow; CompletedBy = userId; }
    public void MarkOverdue() { IsOverdue = true; }
    public void MarkSLABreachNotified() { SLABreachNotifiedAt = DateTime.UtcNow; }
    public void MarkEscalated() { EscalatedAt = DateTime.UtcNow; }
    public void Delegate(int newUserId, int fromTaskId) { AssignedToUserId = newUserId; IsDelegated = true; DelegatedFrom = AssignedToUserId; }
}

// ============================================================
// FILE: src/Domain/Entities/AuditLog.cs
// ============================================================
namespace Darah.ECM.Domain.Entities;

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

    public static AuditLog Create(string eventType, string? entityType = null, string? entityId = null, int? userId = null, string? username = null, string? ipAddress = null, string? oldValues = null, string? newValues = null, string? additionalInfo = null, string severity = "Info", bool isSuccessful = true, string? failureReason = null, string? sessionId = null, string? userAgent = null)
    {
        return new AuditLog
        {
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            Username = username,
            SessionId = sessionId,
            IPAddress = ipAddress,
            UserAgent = userAgent,
            OldValues = oldValues,
            NewValues = newValues,
            AdditionalInfo = additionalInfo,
            Severity = severity,
            IsSuccessful = isSuccessful,
            FailureReason = failureReason,
            CreatedAt = DateTime.UtcNow
        };
    }
}
