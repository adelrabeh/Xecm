using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Entities;

// ─────────────────────────────────────────────────────────────
// USER
// ─────────────────────────────────────────────────────────────
public class User : BaseEntity
{
    public int     UserId               { get; private set; }
    public string  Username             { get; private set; } = string.Empty;
    public string  Email                { get; private set; } = string.Empty;
    public string  PasswordHash         { get; private set; } = string.Empty;
    public string  FullNameAr           { get; private set; } = string.Empty;
    public string? FullNameEn           { get; private set; }
    public int?    DepartmentId         { get; private set; }
    public string? JobTitle             { get; private set; }
    public bool    IsActive             { get; private set; } = true;
    public bool    IsLocked             { get; private set; }
    public DateTime? LockoutEnd         { get; private set; }
    public int     FailedLoginAttempts  { get; private set; }
    public DateTime? LastLoginAt        { get; private set; }
    public string? LastLoginIP          { get; private set; }
    public bool    MFAEnabled           { get; private set; }
    public string  LanguagePreference   { get; private set; } = "ar";
    public bool    MustChangePassword   { get; private set; }
    public string? ExternalId           { get; private set; }

    private User() { }

    public static User Create(string username, string email, string passwordHash,
        string fullNameAr, int createdBy, int? departmentId = null, string? fullNameEn = null)
    {
        var user = new User
        {
            Username    = username.Trim().ToLowerInvariant(),
            Email       = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullNameAr  = fullNameAr,
            FullNameEn  = fullNameEn,
            DepartmentId = departmentId,
            IsActive    = true
        };
        user.SetCreated(createdBy);
        return user;
    }

    public void RecordLogin(string ip)
    {
        LastLoginAt = DateTime.UtcNow; LastLoginIP = ip;
        FailedLoginAttempts = 0; IsLocked = false; LockoutEnd = null;
        SetUpdated(UserId);
    }

    public void IncrementFailedLogin() { FailedLoginAttempts++; SetUpdated(UserId); }

    public void Lock(int durationMinutes)
    {
        IsLocked = true; LockoutEnd = DateTime.UtcNow.AddMinutes(durationMinutes);
        SetUpdated(UserId);
    }

    public void Unlock() { IsLocked = false; LockoutEnd = null; FailedLoginAttempts = 0; SetUpdated(UserId); }

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash; MustChangePassword = false; SetUpdated(UserId);
    }

    public bool IsLockedOut() => IsLocked && LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;
}

// ─────────────────────────────────────────────────────────────
// WORKFLOW INSTANCE
// ─────────────────────────────────────────────────────────────
public class WorkflowInstance : BaseEntity
{
    public int    InstanceId   { get; private set; }
    public int    DefinitionId { get; private set; }
    public Guid   DocumentId   { get; private set; }
    public string Status       { get; private set; } = "InProgress";
    public int?   CurrentStepId { get; private set; }
    public DateTime StartedAt  { get; private set; } = DateTime.UtcNow;
    public int    StartedBy    { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int    Priority     { get; private set; } = 2;

    private WorkflowInstance() { }

    public static WorkflowInstance Start(int definitionId, Guid documentId, int startedBy, int priority = 2)
    {
        var inst = new WorkflowInstance
        {
            DefinitionId = definitionId,
            DocumentId   = documentId,
            StartedBy    = startedBy,
            Priority     = priority,
            StartedAt    = DateTime.UtcNow,
            Status       = "InProgress"
        };
        inst.SetCreated(startedBy);
        return inst;
    }

    public void MoveToStep(int stepId) => CurrentStepId = stepId;
    public void Complete() { Status = "Approved"; CompletedAt = DateTime.UtcNow; }
    public void Reject()   { Status = "Rejected"; CompletedAt = DateTime.UtcNow; }
    public void Cancel(int userId = 0, string? reason = null) { Status = "Cancelled"; CompletedAt = DateTime.UtcNow; }
}

// ─────────────────────────────────────────────────────────────
// WORKFLOW TASK
// ─────────────────────────────────────────────────────────────
public class WorkflowTask : BaseEntity
{
    public int    TaskId           { get; private set; }
    public int    InstanceId       { get; private set; }
    public int    StepId           { get; private set; }
    public int?   AssignedToUserId { get; private set; }
    public int?   AssignedToRoleId { get; private set; }
    public string Status           { get; private set; } = "Pending";
    public DateTime AssignedAt     { get; private set; } = DateTime.UtcNow;
    public DateTime? DueAt         { get; private set; }
    public DateTime? CompletedAt   { get; private set; }
    public int?   CompletedBy      { get; private set; }
    public bool   IsOverdue        { get; private set; }
    public bool   IsDelegated      { get; private set; }
    public int?   DelegatedFrom    { get; private set; }
    public DateTime? EscalatedAt   { get; private set; }
    public DateTime? SLABreachNotifiedAt { get; private set; }

    private WorkflowTask() { }

    public void ClaimBy(int userId)
    {
        AssignedToUserId = userId;
        Status = "InProgress";
    }

    public void ReturnToGroup(int userId, string? comment = null)
    {
        AssignedToUserId = null;
        Status = "Pending";
    }

    public static WorkflowTask Create(int instanceId, int stepId,
        int? userId, int? roleId, int? slaHours)
    {
        var t = new WorkflowTask
        {
            InstanceId       = instanceId,
            StepId           = stepId,
            AssignedToUserId = userId,
            AssignedToRoleId = roleId,
            AssignedAt       = DateTime.UtcNow,
            Status           = "Pending"
        };
        if (slaHours.HasValue) t.DueAt = DateTime.UtcNow.AddHours(slaHours.Value);
        return t;
    }

    public void Complete(int userId)
    {
        Status = "Completed"; CompletedAt = DateTime.UtcNow; CompletedBy = userId;
    }

    public void MarkOverdue()               => IsOverdue = true;
    public void MarkEscalated()             => EscalatedAt = DateTime.UtcNow;
    public void MarkSLABreachNotified()     => SLABreachNotifiedAt = DateTime.UtcNow;
    public void Delegate(int newUserId)     { DelegatedFrom = AssignedToUserId; AssignedToUserId = newUserId; IsDelegated = true; }
}

// ─────────────────────────────────────────────────────────────
// AUDIT LOG  (append-only — no update/delete in EF tracking)
// ─────────────────────────────────────────────────────────────
public class AuditLog
{
    public long    AuditId       { get; private set; }
    public string  EventType     { get; private set; } = string.Empty;
    public string? EntityType    { get; private set; }
    public string? EntityId      { get; private set; }
    public int?    UserId        { get; private set; }
    public string? Username      { get; private set; }
    public string? SessionId     { get; private set; }
    public string? IPAddress     { get; private set; }
    public string? UserAgent     { get; private set; }
    public string? OldValues     { get; private set; }
    public string? NewValues     { get; private set; }
    public string? AdditionalInfo { get; private set; }
    public string  Severity      { get; private set; } = "Info";
    public bool    IsSuccessful  { get; private set; } = true;
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt    { get; private set; } = DateTime.UtcNow;

    private AuditLog() { }

    public static AuditLog Create(string eventType, string? entityType = null,
        string? entityId = null, int? userId = null, string? username = null,
        string? ipAddress = null, string? oldValues = null, string? newValues = null,
        string? additionalInfo = null, string severity = "Info", bool isSuccessful = true,
        string? failureReason = null, string? sessionId = null, string? userAgent = null)
        => new()
        {
            EventType = eventType, EntityType = entityType, EntityId = entityId,
            UserId = userId, Username = username, SessionId = sessionId,
            IPAddress = ipAddress, UserAgent = userAgent,
            OldValues = oldValues, NewValues = newValues, AdditionalInfo = additionalInfo,
            Severity = severity, IsSuccessful = isSuccessful, FailureReason = failureReason,
            CreatedAt = DateTime.UtcNow
        };
}

// ─── USER ROLE ASSIGNMENT ─────────────────────────────────────────────────────
public sealed class UserRoleAssignment
{
    public int  Id       { get; set; }
    public int  UserId   { get; set; }
    public int  RoleId   { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

// ─── ROLE ─────────────────────────────────────────────────────────────────────
public sealed class Role
{
    public int     RoleId   { get; set; }
    public string  RoleCode { get; set; } = string.Empty;
    public string  NameAr   { get; set; } = string.Empty;
    public string? NameEn   { get; set; }
    public bool    IsActive { get; set; } = true;
    public bool    IsSystem { get; set; } = false;
}

// ─── DEPARTMENT ───────────────────────────────────────────────────────────────
public sealed class Department
{
    public int  DepartmentId { get; set; }
    public string NameAr    { get; set; } = string.Empty;
    public string? NameEn   { get; set; }
    public int? ManagerId   { get; set; }
    public int? ParentId    { get; set; }
    public bool IsActive    { get; set; } = true;
}
