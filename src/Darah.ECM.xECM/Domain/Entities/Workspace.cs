using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Events.Workspace;

namespace Darah.ECM.xECM.Domain.Entities;

/// <summary>
/// Business Workspace entity — the core xECM extension concept.
/// Represents a business context container (Project, Contract, Case, etc.)
/// that groups related documents, enforces security, and synchronizes
/// metadata with external systems (SAP, Salesforce, Oracle HR).
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

    // External system binding (core xECM capability)
    public string? ExternalSystemId   { get; private set; }   // e.g. "SAP_PROD"
    public string? ExternalObjectId   { get; private set; }   // e.g. SAP WBS element ID
    public string? ExternalObjectType { get; private set; }   // e.g. "WBSElement"
    public string? ExternalObjectUrl  { get; private set; }   // deep-link back to source
    public DateTime? LastSyncedAt     { get; private set; }
    public string?   SyncStatus       { get; private set; }   // Pending|Synced|Failed|Conflict
    public string?   SyncError        { get; private set; }

    // Lifecycle
    public int       StatusValueId           { get; private set; }
    public int       ClassificationLevelId   { get; private set; } = 2;   // Internal default
    public bool      IsLegalHold             { get; private set; }
    public int?      RetentionPolicyId       { get; private set; }
    public DateOnly? RetentionExpiresAt      { get; private set; }
    public DateTime? ArchivedAt              { get; private set; }
    public int?      ArchivedBy              { get; private set; }

    public bool IsBoundToExternal =>
        !string.IsNullOrEmpty(ExternalSystemId) && !string.IsNullOrEmpty(ExternalObjectId);

    private Workspace() { }   // EF Core

    // ─── Factory ──────────────────────────────────────────────────────────────
    public static Workspace Create(
        string  titleAr,
        int     workspaceTypeId,
        string  workspaceTypeCode,
        int     ownerId,
        int     statusValueId,
        string  workspaceNumber,
        int     createdBy,
        string? titleEn           = null,
        int?    departmentId      = null,
        string? description       = null,
        int     classificationLevelId = 2,
        int?    retentionPolicyId = null)
    {
        var ws = new Workspace
        {
            WorkspaceId          = Guid.NewGuid(),
            WorkspaceNumber      = workspaceNumber,
            TitleAr              = titleAr,
            TitleEn              = titleEn,
            WorkspaceTypeId      = workspaceTypeId,
            OwnerId              = ownerId,
            DepartmentId         = departmentId,
            Description          = description,
            StatusValueId        = statusValueId,
            ClassificationLevelId = classificationLevelId,
            RetentionPolicyId    = retentionPolicyId
        };
        ws.SetCreated(createdBy);
        ws.RaiseDomainEvent(new WorkspaceCreatedEvent(
            ws.WorkspaceId, workspaceNumber, workspaceTypeCode, createdBy));
        return ws;
    }

    // ─── External binding ────────────────────────────────────────────────────
    public void BindToExternal(string systemId, string objectId,
        string objectType, string? objectUrl, int userId)
    {
        if (IsBoundToExternal)
            throw new InvalidOperationException(
                $"مساحة العمل مرتبطة بالفعل بـ {ExternalSystemId}/{ExternalObjectId}.");

        ExternalSystemId   = systemId;
        ExternalObjectId   = objectId;
        ExternalObjectType = objectType;
        ExternalObjectUrl  = objectUrl;
        SyncStatus         = "Pending";
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceLinkedToExternalEvent(
            WorkspaceId, systemId, objectId, objectType, userId));
    }

    public void RecordSyncSuccess(DateTime syncedAt)
    {
        LastSyncedAt = syncedAt;
        SyncStatus   = "Synced";
        SyncError    = null;
    }

    public void RecordSyncFailure(string error)
    {
        SyncStatus = "Failed";
        SyncError  = error;
    }

    public void RecordSyncConflict() => SyncStatus = "Conflict";

    // ─── Lifecycle ───────────────────────────────────────────────────────────
    public void Archive(int archivedBy)
    {
        ArchivedAt = DateTime.UtcNow;
        ArchivedBy = archivedBy;
        SetUpdated(archivedBy);
        RaiseDomainEvent(new WorkspaceArchivedEvent(WorkspaceId, archivedBy));
    }

    public void ApplyLegalHold(int appliedBy)
    {
        IsLegalHold = true;
        RaiseDomainEvent(new WorkspaceLegalHoldAppliedEvent(WorkspaceId, appliedBy, 0));
    }

    public void ReleaseLegalHold() => IsLegalHold = false;

    public void UpdateTitle(string titleAr, string? titleEn, int userId)
    {
        TitleAr = titleAr;
        TitleEn = titleEn;
        SetUpdated(userId);
    }

    public void SetRetentionExpiry(DateOnly expiry, int userId)
    {
        RetentionExpiresAt = expiry;
        SetUpdated(userId);
    }

    public void UpdateStatus(int statusValueId, int userId)
    {
        StatusValueId = statusValueId;
        SetUpdated(userId);
    }
}
