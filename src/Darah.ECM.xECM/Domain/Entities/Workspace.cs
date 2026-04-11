using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.xECM.Domain.Events;
using Darah.ECM.xECM.Domain.ValueObjects;

namespace Darah.ECM.xECM.Domain.Entities;

/// <summary>
/// Business Workspace — first-class Aggregate Root in xECM.
///
/// NOT a folder. A governed business context boundary that:
///   - Represents a real business entity (Project, Contract, Case, Customer, Employee)
///   - Governs all bound documents (security, classification, retention, workflow)
///   - Can bind to an external ERP/CRM object (one-to-one enforced)
///   - Has its own lifecycle that cascades to bound documents
/// </summary>
public sealed class Workspace : BaseEntity, IAggregateRoot
{
    public Guid            WorkspaceId         { get; private set; }
    public string          WorkspaceNumber     { get; private set; } = string.Empty;
    public int             WorkspaceTypeId     { get; private set; }
    public string          TitleAr             { get; private set; } = string.Empty;
    public string?         TitleEn             { get; private set; }
    public string?         Description         { get; private set; }
    public int             OwnerId             { get; private set; }
    public int?            DepartmentId        { get; private set; }
    public WorkspaceStatus Status              { get; private set; } = WorkspaceStatus.Draft;
    public ClassificationLevel Classification { get; private set; } = ClassificationLevel.Internal;
    public int?            RetentionPolicyId   { get; private set; }
    public int?            DefaultWorkflowId   { get; private set; }
    public DateOnly?       RetentionExpiresAt  { get; private set; }
    public int             Priority            { get; private set; } = 2;

    // External binding
    public string?  ExternalSystemCode  { get; private set; }
    public string?  ExternalObjectId    { get; private set; }
    public string?  ExternalObjectType  { get; private set; }
    public string?  ExternalObjectUrl   { get; private set; }
    public string?  ExternalObjectTitle { get; private set; }
    public DateTime? LastSyncedAt       { get; private set; }
    public string?  SyncStatus          { get; private set; }
    public string?  SyncError           { get; private set; }
    public int      SyncAttempts        { get; private set; }

    // Legal hold
    public bool     IsLegalHold  { get; private set; }
    public DateTime? LegalHoldAt { get; private set; }
    public int?     LegalHoldBy  { get; private set; }

    // Governance counts
    public int DocumentCount { get; private set; }

    public bool IsBoundToExternal =>
        !string.IsNullOrEmpty(ExternalSystemCode) && !string.IsNullOrEmpty(ExternalObjectId);

    private Workspace() { }

    public static Workspace Create(
        string titleAr, int workspaceTypeId, int ownerId, string workspaceNumber,
        int createdBy, string? titleEn = null, int? departmentId = null,
        string? description = null, ClassificationLevel? classification = null,
        int? retentionPolicyId = null, int? defaultWorkflowId = null, int priority = 2)
    {
        var ws = new Workspace
        {
            WorkspaceId       = Guid.NewGuid(),
            WorkspaceNumber   = workspaceNumber,
            WorkspaceTypeId   = workspaceTypeId,
            TitleAr           = titleAr.Trim(),
            TitleEn           = titleEn?.Trim(),
            Description       = description,
            OwnerId           = ownerId,
            DepartmentId      = departmentId,
            Status            = WorkspaceStatus.Draft,
            Classification    = classification ?? ClassificationLevel.Internal,
            RetentionPolicyId = retentionPolicyId,
            DefaultWorkflowId = defaultWorkflowId,
            Priority          = priority
        };
        ws.SetCreated(createdBy);
        ws.RaiseDomainEvent(new WorkspaceCreatedEvent(
            ws.WorkspaceId, ws.WorkspaceNumber, ws.TitleAr, workspaceTypeId, createdBy));
        return ws;
    }

    // Lifecycle
    public void Activate(int userId)
    {
        EnsureCanTransitionTo(WorkspaceStatus.Active);
        Status = WorkspaceStatus.Active;
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceActivatedEvent(WorkspaceId, userId));
    }

    public void Close(int userId, string? reason = null)
    {
        EnsureCanTransitionTo(WorkspaceStatus.Closed);
        Status = WorkspaceStatus.Closed;
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceClosedEvent(WorkspaceId, userId, reason));
    }

    public void Archive(int userId)
    {
        if (IsLegalHold) throw new InvalidOperationException("لا يمكن أرشفة مساحة عمل خاضعة لتجميد قانوني");
        EnsureCanTransitionTo(WorkspaceStatus.Archived);
        Status = WorkspaceStatus.Archived;
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceArchivedEvent(WorkspaceId, userId, DocumentCount));
    }

    public void Dispose(int userId)
    {
        if (IsLegalHold) throw new InvalidOperationException("لا يمكن إتلاف مساحة عمل خاضعة لتجميد قانوني");
        EnsureCanTransitionTo(WorkspaceStatus.Disposed);
        Status = WorkspaceStatus.Disposed;
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceDisposedEvent(WorkspaceId, userId));
    }

    private void EnsureCanTransitionTo(WorkspaceStatus target)
    {
        if (!Status.CanTransitionTo(target))
            throw new InvalidOperationException($"الانتقال من '{Status}' إلى '{target}' غير مسموح");
    }

    // Legal hold
    public void ApplyLegalHold(int appliedBy)
    {
        if (Status == WorkspaceStatus.Disposed) throw new InvalidOperationException("مساحة العمل متلفة");
        IsLegalHold = true; LegalHoldAt = DateTime.UtcNow; LegalHoldBy = appliedBy;
        SetUpdated(appliedBy);
        RaiseDomainEvent(new WorkspaceLegalHoldAppliedEvent(WorkspaceId, appliedBy, DocumentCount));
    }

    public void ReleaseLegalHold(int releasedBy)
    {
        if (!IsLegalHold) throw new InvalidOperationException("لا يوجد تجميد قانوني نشط");
        IsLegalHold = false; LegalHoldAt = null; LegalHoldBy = null;
        SetUpdated(releasedBy);
        RaiseDomainEvent(new WorkspaceLegalHoldReleasedEvent(WorkspaceId, releasedBy));
    }

    // Document binding governance
    public void ValidateCanBindDocument()
    {
        if (!Status.AllowsNewDocuments)
            throw new InvalidOperationException($"لا يمكن إضافة وثائق لمساحة عمل بحالة '{Status}'");
        if (IsLegalHold)
            throw new InvalidOperationException("لا يمكن إضافة وثائق لمساحة عمل خاضعة لتجميد قانوني");
    }

    public void IncrementDocumentCount() => DocumentCount++;
    public void DecrementDocumentCount() { if (DocumentCount > 0) DocumentCount--; }

    // External binding (Sprint 6)
    public void BindToExternalObject(string systemCode, string objectId, string objectType,
        string? objectUrl, string? objectTitle, int userId)
    {
        if (IsBoundToExternal)
            throw new InvalidOperationException(
                $"مساحة العمل مرتبطة بالفعل بـ {ExternalSystemCode}/{ExternalObjectId}");
        ExternalSystemCode = systemCode; ExternalObjectId = objectId;
        ExternalObjectType = objectType; ExternalObjectUrl = objectUrl;
        ExternalObjectTitle = objectTitle; SyncStatus = "Pending"; SyncAttempts = 0;
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceLinkedToExternalEvent(
            WorkspaceId, systemCode, objectId, objectType, userId));
    }

    public void UnbindExternalObject(int userId)
    {
        if (!IsBoundToExternal) throw new InvalidOperationException("غير مرتبطة بكيان خارجي");
        ExternalSystemCode = null; ExternalObjectId = null; ExternalObjectType = null;
        ExternalObjectUrl = null; ExternalObjectTitle = null; SyncStatus = null;
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceExternalBindingRemovedEvent(WorkspaceId, userId));
    }

    public void BeginSync(int userId)
    {
        if (!IsBoundToExternal) throw new InvalidOperationException("غير مرتبطة بكيان خارجي");
        SyncStatus = "Syncing"; SyncAttempts++; SetUpdated(userId);
    }

    public void RecordSyncSuccess(DateTime at, int fieldsUpdated, int userId)
    {
        LastSyncedAt = at; SyncStatus = "Synced"; SyncError = null; SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceSyncCompletedEvent(WorkspaceId, ExternalSystemCode!, fieldsUpdated, 0));
    }

    public void RecordSyncFailure(string error, int userId)
    {
        SyncStatus = "Failed"; SyncError = error; SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceSyncFailedEvent(WorkspaceId, ExternalSystemCode!, error));
    }

    public void RecordSyncConflict(int conflicts, int userId)
    {
        SyncStatus = "Conflict"; SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceSyncCompletedEvent(WorkspaceId, ExternalSystemCode!, 0, conflicts));
    }

    // Governance
    public void UpdateClassification(ClassificationLevel level, int userId)
    {
        bool becameMoreRestrictive = level.IsMoreRestrictiveThan(Classification);
        Classification = level; SetUpdated(userId);
        if (becameMoreRestrictive)
            RaiseDomainEvent(new WorkspaceClassificationChangedEvent(WorkspaceId, level.Code, userId));
    }

    public void UpdateContent(string titleAr, string? titleEn, string? description, int userId)
    { TitleAr = titleAr.Trim(); TitleEn = titleEn?.Trim(); Description = description; SetUpdated(userId); }

    public void SetRetentionPolicy(int? policyId, DateOnly? expiry, int userId)
    { RetentionPolicyId = policyId; RetentionExpiresAt = expiry; SetUpdated(userId); }

    public void SetDefaultWorkflow(int? workflowId, int userId)
    { DefaultWorkflowId = workflowId; SetUpdated(userId); }

    public bool CanBeDeleted() => Status == WorkspaceStatus.Draft && DocumentCount == 0 && !IsLegalHold;
    public bool NeedsSync() => IsBoundToExternal && SyncStatus is "Pending" or "Failed" && SyncAttempts < 5;
}
