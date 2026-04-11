using Darah.ECM.Domain.Events.Workspace;
using Darah.ECM.xECM.Domain.Entities;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.Entities;

public sealed class WorkspaceEntityTests
{
    private static Workspace MakeWorkspace()
        => Workspace.Create("مشروع الاختبار", 1, 1, 1, "WS-2026-00001", 1);

    // ── Factory ───────────────────────────────────────────────────
    [Fact]
    public void Create_RaisesWorkspaceCreatedEvent()
    {
        var ws = MakeWorkspace();
        Assert.Single(ws.DomainEvents);
        Assert.IsType<WorkspaceCreatedEvent>(ws.DomainEvents.First());
    }

    [Fact]
    public void Create_WorkspaceId_IsNotEmpty()
        => Assert.NotEqual(Guid.Empty, MakeWorkspace().WorkspaceId);

    [Fact]
    public void Create_IsBoundToExternal_IsFalse()
        => Assert.False(MakeWorkspace().IsBoundToExternal);

    // ── External binding ──────────────────────────────────────────
    [Fact]
    public void BindToExternal_SetsAllFields()
    {
        var ws = MakeWorkspace();
        ws.BindToExternal("SAP_PROD", "WBS-001", "WBSElement", "http://sap/wbs/001", 1);

        Assert.True(ws.IsBoundToExternal);
        Assert.Equal("SAP_PROD", ws.ExternalSystemId);
        Assert.Equal("WBS-001", ws.ExternalObjectId);
        Assert.Equal("WBSElement", ws.ExternalObjectType);
        Assert.Equal("Pending", ws.SyncStatus);
    }

    [Fact]
    public void BindToExternal_RaisesLinkedEvent()
    {
        var ws = MakeWorkspace();
        ws.BindToExternal("SAP_PROD", "WBS-001", "WBSElement", null, 1);
        var ev = ws.DomainEvents.OfType<WorkspaceLinkedToExternalEvent>().FirstOrDefault();
        Assert.NotNull(ev);
        Assert.Equal("SAP_PROD", ev.ExternalSystemId);
    }

    [Fact]
    public void BindToExternal_AlreadyBound_Throws()
    {
        var ws = MakeWorkspace();
        ws.BindToExternal("SAP_PROD", "WBS-001", "WBSElement", null, 1);
        Assert.Throws<InvalidOperationException>(
            () => ws.BindToExternal("SF_CRM", "ACC-002", "Account", null, 1));
    }

    // ── Sync status ───────────────────────────────────────────────
    [Fact]
    public void RecordSyncSuccess_SetsSyncedStatus()
    {
        var ws = MakeWorkspace();
        ws.BindToExternal("SAP_PROD", "WBS-001", "WBSElement", null, 1);
        var now = DateTime.UtcNow;
        ws.RecordSyncSuccess(now, 1);
        Assert.Equal("Synced", ws.SyncStatus);
        Assert.Equal(now, ws.LastSyncedAt);
        Assert.Null(ws.SyncError);
    }

    [Fact]
    public void RecordSyncFailure_SetsFailedStatusAndError()
    {
        var ws = MakeWorkspace();
        ws.RecordSyncFailure("Connection timeout", 1);
        Assert.Equal("Failed", ws.SyncStatus);
        Assert.Equal("Connection timeout", ws.SyncError);
    }

    [Fact]
    public void RecordSyncConflict_SetsConflictStatus()
    {
        var ws = MakeWorkspace();
        ws.RecordSyncConflict();
        Assert.Equal("Conflict", ws.SyncStatus);
    }

    // ── Legal hold ────────────────────────────────────────────────
    [Fact]
    public void ApplyLegalHold_SetsFlag_RaisesEvent()
    {
        var ws = MakeWorkspace();
        ws.ApplyLegalHold(1);
        Assert.True(ws.IsLegalHold);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceLegalHoldAppliedEvent);
    }

    [Fact]
    public void ReleaseLegalHold_ClearsFlag()
    {
        var ws = MakeWorkspace();
        ws.ApplyLegalHold(1);
        ws.ReleaseLegalHold();
        Assert.False(ws.IsLegalHold);
    }

    // ── Archive ───────────────────────────────────────────────────
    [Fact]
    public void Archive_SetsTimestamp_RaisesEvent()
    {
        var ws = MakeWorkspace();
        ws.Archive(1);
        Assert.NotNull(ws.ArchivedAt);
        Assert.Equal(1, ws.ArchivedBy);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceArchivedEvent);
    }

    // ── Retention ────────────────────────────────────────────────
    [Fact]
    public void SetRetentionExpiry_StoresDate()
    {
        var ws = MakeWorkspace();
        var expiry = new DateOnly(2030, 12, 31);
        ws.SetRetentionExpiry(expiry, 1);
        Assert.Equal(expiry, ws.RetentionExpiresAt);
    }

    // ── Title update ─────────────────────────────────────────────
    [Fact]
    public void UpdateTitle_ChangesBothLanguages()
    {
        var ws = MakeWorkspace();
        ws.UpdateTitle("عنوان جديد", "New Title", 1);
        Assert.Equal("عنوان جديد", ws.TitleAr);
        Assert.Equal("New Title", ws.TitleEn);
    }
}
