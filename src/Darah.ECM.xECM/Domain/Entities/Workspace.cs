using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Events.Workspace;

namespace Darah.ECM.xECM.Domain.Entities;

/// <summary>
/// Business Workspace — the core xECM entity.
/// Governs documents, metadata, security, and lifecycle within a business context
/// (Project, Contract, Case, Customer, Employee, Department).
/// </summary>
public sealed class Workspace : BaseEntity
{
    public Guid    WorkspaceId        { get; private set; }
    public string  WorkspaceNumber    { get; private set; } = string.Empty;
    public int     WorkspaceTypeId    { get; private set; }
    public string  TitleAr            { get; private set; } = string.Empty;
    public string? TitleEn            { get; private set; }
    public string? Description        { get; private set; }
    public int     OwnerId            { get; private set; }
    public int?    DepartmentId       { get; private set; }
    public string?   ExternalSystemId   { get; private set; }
    public string?   ExternalObjectId   { get; private set; }
    public string?   ExternalObjectType { get; private set; }
    public string?   ExternalObjectUrl  { get; private set; }
    public DateTime? LastSyncedAt       { get; private set; }
    public string?   SyncStatus         { get; private set; }
    public string?   SyncError          { get; private set; }
    public int     StatusValueId         { get; private set; }
    public int     ClassificationLevelId { get; private set; } = 2;
    public bool    IsLegalHold           { get; private set; }
    public int?    RetentionPolicyId     { get; private set; }
    public DateOnly? RetentionExpiresAt  { get; private set; }
    public DateTime? ArchivedAt          { get; private set; }
    public int?    ArchivedBy            { get; private set; }

    public bool IsBoundToExternal
        => !string.IsNullOrEmpty(ExternalSystemId) && !string.IsNullOrEmpty(ExternalObjectId);

    private Workspace() { }

    public static Workspace Create(string titleAr, int workspaceTypeId, int ownerId,
        int statusValueId, string workspaceNumber, int createdBy,
        string? titleEn = null, int? departmentId = null, string? description = null,
        int classificationLevelId = 2, int? retentionPolicyId = null)
    {
        var ws = new Workspace
        {
            WorkspaceId = Guid.NewGuid(), WorkspaceNumber = workspaceNumber,
            TitleAr = titleAr, TitleEn = titleEn, WorkspaceTypeId = workspaceTypeId,
            OwnerId = ownerId, DepartmentId = departmentId, Description = description,
            StatusValueId = statusValueId, ClassificationLevelId = classificationLevelId,
            RetentionPolicyId = retentionPolicyId
        };
        ws.SetCreated(createdBy);
        ws.RaiseDomainEvent(new WorkspaceCreatedEvent(ws.WorkspaceId, workspaceNumber, string.Empty, createdBy));
        return ws;
    }

    public void BindToExternal(string systemId, string objectId, string objectType, string? objectUrl, int userId)
    {
        if (IsBoundToExternal) throw new InvalidOperationException($"Already bound to {ExternalSystemId}/{ExternalObjectId}.");
        ExternalSystemId = systemId; ExternalObjectId = objectId;
        ExternalObjectType = objectType; ExternalObjectUrl = objectUrl; SyncStatus = "Pending";
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceLinkedToExternalEvent(WorkspaceId, systemId, objectId, objectType, userId));
    }

    public void RecordSyncSuccess(DateTime at, int userId) { LastSyncedAt = at; SyncStatus = "Synced"; SyncError = null; SetUpdated(userId); }
    public void RecordSyncFailure(string error, int userId) { SyncStatus = "Failed"; SyncError = error; SetUpdated(userId); }
    public void RecordSyncConflict() => SyncStatus = "Conflict";

    public void Archive(int by) { ArchivedAt = DateTime.UtcNow; ArchivedBy = by; SetUpdated(by); RaiseDomainEvent(new WorkspaceArchivedEvent(WorkspaceId, by)); }
    public void ApplyLegalHold(int by) { IsLegalHold = true; RaiseDomainEvent(new WorkspaceLegalHoldAppliedEvent(WorkspaceId, by, 0)); }
    public void ReleaseLegalHold() => IsLegalHold = false;
    public void SetRetentionExpiry(DateOnly expiry, int userId) { RetentionExpiresAt = expiry; SetUpdated(userId); }
    public void UpdateTitle(string titleAr, string? titleEn, int userId) { TitleAr = titleAr; TitleEn = titleEn; SetUpdated(userId); }
}
