using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
// PARTITION (Multi-Tenancy)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Logical tenant/partition — isolates data, users, and config.</summary>
public sealed class Partition : BaseEntity
{
    public int      PartitionId   { get; private set; }
    public string   Code          { get; private set; } = string.Empty;
    public string   NameAr        { get; private set; } = string.Empty;
    public string?  NameEn        { get; private set; }
    public string?  LogoUrl       { get; private set; }
    public bool     IsActive      { get; private set; } = true;
    public string   AuthHandler   { get; private set; } = "Local"; // Local|LDAP|OAuth
    public string?  AuthConfig    { get; private set; }            // JSON config
    public int?     ParentId      { get; private set; }            // Sub-partition
    public string?  AdminEmail    { get; private set; }

    private Partition() { }

    public static Partition Create(string code, string nameAr, int createdBy,
        string? nameEn = null, string authHandler = "Local")
    {
        var p = new Partition
        {
            Code = code.ToUpperInvariant(), NameAr = nameAr,
            NameEn = nameEn, AuthHandler = authHandler
        };
        p.SetCreated(createdBy);
        return p;
    }

    public void UpdateAuth(string handler, string? config)
    {
        AuthHandler = handler;
        AuthConfig  = config;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DOCUMENT-CENTRIC TASK
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>A task created from or linked to a document.</summary>
public sealed class DocumentTask : BaseEntity
{
    public long    TaskId          { get; private set; }
    public string  TraceId         { get; private set; } = Guid.NewGuid().ToString("N"); // Immutable unique ID
    public Guid?   DocumentId      { get; private set; }  // Linked document (optional)
    public string  Title           { get; private set; } = string.Empty;
    public string? Description     { get; private set; }
    public string  TaskType        { get; private set; } = "Review";  // Review|Approve|Edit|Sign|Process
    public string  Status          { get; private set; } = "Open";    // Open|InProgress|Done|Rejected|Cancelled
    public string  Priority        { get; private set; } = "Normal";  // Low|Normal|High|Urgent
    public string  RoutingType     { get; private set; } = "Sequential"; // Sequential|Parallel|AdHoc
    public int?    AssignedToUserId{ get; private set; }
    public int?    AssignedToGroupId{ get; private set; }
    public DateTime? DueDate       { get; private set; }
    public DateTime? CompletedAt   { get; private set; }
    public int?    CompletedBy     { get; private set; }
    public string? Resolution      { get; private set; } // Approve|Reject|Edit|Comment
    public string? ResolutionNote  { get; private set; }
    public int?    PartitionId     { get; private set; }
    public int?    ParentTaskId    { get; private set; } // For sub-tasks
    public bool    IsOverdue       { get; private set; }
    public string? ApplicationRole { get; private set; } // Reviewer|Approver|CaseManager

    private DocumentTask() { }

    public static DocumentTask Create(string title, int createdBy,
        Guid? documentId = null, string taskType = "Review",
        string priority = "Normal", DateTime? dueDate = null,
        int? assignedTo = null, int? partitionId = null)
    {
        var t = new DocumentTask
        {
            Title = title, DocumentId = documentId, TaskType = taskType,
            Priority = priority, DueDate = dueDate,
            AssignedToUserId = assignedTo, PartitionId = partitionId,
            TraceId = Guid.NewGuid().ToString("N"),
            Status = "Open"
        };
        t.SetCreated(createdBy);
        return t;
    }

    public void Assign(int userId, string? appRole = null)
    {
        AssignedToUserId = userId;
        ApplicationRole  = appRole;
        Status = "InProgress";
    }

    public void Complete(int byUserId, string resolution, string? note = null)
    {
        Status = resolution == "Approve" ? "Done" : resolution == "Reject" ? "Rejected" : "Done";
        Resolution = resolution; ResolutionNote = note;
        CompletedAt = DateTime.UtcNow; CompletedBy = byUserId;
    }

    public void Reassign(int toUserId, string? instructions = null)
    {
        AssignedToUserId = toUserId;
        ResolutionNote = instructions;
        Status = "Open";
    }
}

/// <summary>Comment on a document task.</summary>
public sealed class TaskComment : BaseEntity
{
    public long    CommentId  { get; private set; }
    public long    TaskId     { get; private set; }
    public string  Body       { get; private set; } = string.Empty;
    public bool    IsInternal { get; private set; }  // Internal/External visibility
    public string? AttachmentUrl { get; private set; }

    private TaskComment() { }

    public static TaskComment Create(long taskId, string body, int userId, bool isInternal = false)
    {
        var c = new TaskComment { TaskId = taskId, Body = body, IsInternal = isInternal };
        c.SetCreated(userId);
        return c;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// APPLICATION ROLES (Functional — separate from Access Roles)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Functional role within workflows — e.g. Reviewer, Approver, CaseManager.</summary>
public sealed class ApplicationRole : BaseEntity
{
    public int    AppRoleId   { get; private set; }
    public string Code        { get; private set; } = string.Empty;
    public string NameAr      { get; private set; } = string.Empty;
    public string? NameEn     { get; private set; }
    public string? Description{ get; private set; }
    public bool   IsSystem    { get; private set; }  // Cannot be deleted
    public int?   PartitionId { get; private set; }

    private ApplicationRole() { }

    public static ApplicationRole Create(string code, string nameAr,
        int createdBy, bool isSystem = false, int? partitionId = null)
    {
        var r = new ApplicationRole
        {
            Code = code, NameAr = nameAr, IsSystem = isSystem, PartitionId = partitionId
        };
        r.SetCreated(createdBy);
        return r;
    }
}

/// <summary>User-to-ApplicationRole mapping.</summary>
public sealed class UserApplicationRole
{
    public int  Id            { get; set; }
    public int  UserId        { get; set; }
    public int  AppRoleId     { get; set; }
    public int? PartitionId   { get; set; }
    public bool IsActive      { get; set; } = true;
    public DateTime AssignedAt{ get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════════════════════
// OAUTH CLIENT (External Integration)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Registered external application for OAuth 2.0 access.</summary>
public sealed class OAuthClient : BaseEntity
{
    public int      ClientId      { get; private set; }
    public string   ClientKey     { get; private set; } = string.Empty; // client_id
    public string   ClientSecret  { get; private set; } = string.Empty; // hashed
    public string   Name          { get; private set; } = string.Empty;
    public string?  Description   { get; private set; }
    public string   Scopes        { get; private set; } = string.Empty; // comma-separated
    public string?  RedirectUris  { get; private set; }
    public bool     IsActive      { get; private set; } = true;
    public int?     PartitionId   { get; private set; }
    public DateTime? ExpiresAt    { get; private set; }

    private OAuthClient() { }

    public static OAuthClient Create(string name, string scopes, int createdBy, int? partitionId = null)
    {
        var c = new OAuthClient
        {
            Name = name, Scopes = scopes, PartitionId = partitionId,
            ClientKey    = Guid.NewGuid().ToString("N"),
            ClientSecret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void Rotate() =>
        ClientSecret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
}

// ═══════════════════════════════════════════════════════════════════════════════
// SYSTEM CONFIGURATION
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Centralized configuration — global or per-partition.</summary>
public sealed class SystemConfig : BaseEntity
{
    public int      ConfigId    { get; private set; }
    public string   Key         { get; private set; } = string.Empty;
    public string   Value       { get; private set; } = string.Empty;
    public string?  Description { get; private set; }
    public string   Category    { get; private set; } = "General"; // General|Storage|Workflow|Security
    public bool     IsReadOnly  { get; private set; }
    public int?     PartitionId { get; private set; }  // null = global

    private SystemConfig() { }

    public static SystemConfig Create(string key, string value, string category,
        int createdBy, int? partitionId = null, bool readOnly = false)
    {
        var c = new SystemConfig
        {
            Key = key, Value = value, Category = category,
            PartitionId = partitionId, IsReadOnly = readOnly
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void Update(string value, int updatedBy)
    {
        if (IsReadOnly) throw new InvalidOperationException("Read-only config cannot be changed");
        Value = value;
        SetUpdated(updatedBy);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// RECYCLE BIN ENTRY
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Tracks soft-deleted documents for restore or permanent deletion.</summary>
public sealed class RecycleBinEntry : BaseEntity
{
    public long   EntryId      { get; private set; }
    public Guid   DocumentId   { get; private set; }
    public string DocumentTitle{ get; private set; } = string.Empty;
    public string DeletedBy    { get; private set; } = string.Empty;
    public DateTime ExpiresAt  { get; private set; }  // Auto-purge date
    public string? Reason      { get; private set; }
    public bool   IsPermanent  { get; private set; }

    private RecycleBinEntry() { }

    public static RecycleBinEntry Create(Guid documentId, string title, int deletedBy, string? reason = null)
    {
        var e = new RecycleBinEntry
        {
            DocumentId = documentId, DocumentTitle = title,
            DeletedBy = deletedBy.ToString(), ExpiresAt = DateTime.UtcNow.AddDays(30),
            Reason = reason
        };
        e.SetCreated(deletedBy);
        return e;
    }

    public void MarkPermanent() => IsPermanent = true;
}

// ═══════════════════════════════════════════════════════════════════════════════
// USER GROUP (for task assignment)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>User group for team-based task assignment and routing.</summary>
public sealed class UserGroup : BaseEntity
{
    public int    GroupId     { get; private set; }
    public string Code        { get; private set; } = string.Empty;
    public string NameAr      { get; private set; } = string.Empty;
    public string? NameEn     { get; private set; }
    public int?   ManagerId   { get; private set; }
    public int?   ParentId    { get; private set; } // Hierarchical groups
    public int?   PartitionId { get; private set; }
    public bool   IsDynamic   { get; private set; } // Rule-based membership
    public string? MemberRule  { get; private set; } // JSON rule for dynamic groups
    public bool   IsActive    { get; private set; } = true;

    private UserGroup() { }

    public static UserGroup Create(string code, string nameAr, int createdBy,
        int? partitionId = null, bool isDynamic = false)
    {
        var g = new UserGroup
        {
            Code = code, NameAr = nameAr, PartitionId = partitionId, IsDynamic = isDynamic
        };
        g.SetCreated(createdBy);
        return g;
    }
}

/// <summary>User-to-Group membership.</summary>
public sealed class GroupMember
{
    public int  MemberId    { get; set; }
    public int  GroupId     { get; set; }
    public int  UserId      { get; set; }
    public bool IsManager   { get; set; }
    public DateTime JoinedAt{ get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════════════════════
// ESCALATION MODEL — Hierarchy-Aware, Permission-Based
// ═══════════════════════════════════════════════════════════════════════════════

public enum UserRole
{
    Viewer            = 0,
    Employee          = 1,
    Supervisor        = 2,
    DepartmentManager = 3,
    SystemAdmin       = 4,
}

public enum EscalationStatus
{
    Pending    = 0,  // Awaiting action from escalated-to user
    Accepted   = 1,  // Acknowledged by receiving party
    Rejected   = 2,  // Returned to original assignee
    Resolved   = 3,  // Escalation resolved
    Cancelled  = 4,  // Withdrawn by initiator
}

public enum EscalationLevel
{
    ToSupervisor        = 1,  // Employee → Supervisor
    ToDepartmentManager = 2,  // Supervisor → Dept Manager
    ToCrossManager      = 3,  // Dept Manager → peer/higher
}

/// <summary>
/// Represents one escalation event on a task.
/// A task may have multiple escalation records over its lifecycle.
/// </summary>
public sealed class TaskEscalation
{
    public long             EscalationId       { get; private set; }
    public int              TaskId             { get; private set; }
    public int              EscalatedFromUserId{ get; private set; }
    public int              EscalatedToUserId  { get; private set; }
    public UserRole         EscalatedToRole    { get; private set; }
    public EscalationLevel  EscalationLevel    { get; private set; }
    public EscalationStatus Status             { get; private set; } = EscalationStatus.Pending;
    public string?          Reason             { get; private set; }
    public string?          Department         { get; private set; }
    public DateTime         EscalatedAt        { get; private set; } = DateTime.UtcNow;
    public DateTime?        ResolvedAt         { get; private set; }
    public string?          ResolutionNote     { get; private set; }
    public int?             ResolvedByUserId   { get; private set; }

    private TaskEscalation() {}

    public static TaskEscalation Create(
        int taskId, int fromUserId, int toUserId,
        UserRole toRole, EscalationLevel level,
        string? reason, string? department)
    {
        return new TaskEscalation
        {
            TaskId              = taskId,
            EscalatedFromUserId = fromUserId,
            EscalatedToUserId   = toUserId,
            EscalatedToRole     = toRole,
            EscalationLevel     = level,
            Reason              = reason,
            Department          = department,
            EscalatedAt         = DateTime.UtcNow,
            Status              = EscalationStatus.Pending,
        };
    }

    public void Accept(int byUserId) =>
        Apply(EscalationStatus.Accepted, byUserId);

    public void Reject(int byUserId, string note) =>
        Apply(EscalationStatus.Rejected, byUserId, note);

    public void Resolve(int byUserId, string note) =>
        Apply(EscalationStatus.Resolved, byUserId, note);

    private void Apply(EscalationStatus s, int userId, string? note = null)
    {
        Status = s; ResolvedAt = DateTime.UtcNow;
        ResolvedByUserId = userId; ResolutionNote = note;
    }
}

/// <summary>
/// Hierarchy-aware escalation validation service.
/// </summary>
public static class EscalationPolicy
{
    /// <summary>
    /// Returns true if fromRole can escalate to toRole in the same department context.
    /// </summary>
    public static bool CanEscalate(UserRole fromRole, UserRole toRole, bool sameDepartment)
    {
        return (fromRole, toRole, sameDepartment) switch
        {
            // Employee can only escalate to their Supervisor (same dept)
            (UserRole.Employee, UserRole.Supervisor, true)               => true,
            // Supervisor can escalate to DeptManager (same dept)
            (UserRole.Supervisor, UserRole.DepartmentManager, true)      => true,
            // DeptManager can escalate to another DeptManager or higher (cross-dept)
            (UserRole.DepartmentManager, UserRole.DepartmentManager, _)  => true,
            // SystemAdmin does not participate in operational escalation
            (_, UserRole.SystemAdmin, _)                                  => false,
            _                                                             => false,
        };
    }

    public static EscalationLevel GetLevel(UserRole fromRole) => fromRole switch
    {
        UserRole.Employee          => EscalationLevel.ToSupervisor,
        UserRole.Supervisor        => EscalationLevel.ToDepartmentManager,
        UserRole.DepartmentManager => EscalationLevel.ToCrossManager,
        _                          => EscalationLevel.ToSupervisor,
    };

    public static string GetDenialReason(UserRole fromRole, UserRole toRole) =>
        (fromRole, toRole) switch
        {
            (UserRole.Employee, UserRole.DepartmentManager) =>
                "الموظف لا يستطيع التصعيد مباشرة لمدير القسم — يجب أن يمر عبر المشرف أولاً.",
            (UserRole.Employee, UserRole.SystemAdmin) =>
                "لا يمكن التصعيد لمدير النظام — هذا ليس مسار تشغيلي.",
            (_, UserRole.SystemAdmin) =>
                "مدير النظام ليس جزءاً من مسار التصعيد التشغيلي.",
            _ => "ليس لديك صلاحية هذا التصعيد.",
        };
}
