using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.xECM.Domain.Entities;
using Darah.ECM.xECM.Domain.Events;
using Darah.ECM.xECM.Domain.Services;
using Darah.ECM.xECM.Domain.ValueObjects;
using Xunit;
using Darah.ECM.Domain.Services;
using Darah.ECM.Domain.Entities;

namespace Darah.ECM.UnitTests.xECM.Domain;

// ─── WORKSPACE STATUS VALUE OBJECT TESTS ─────────────────────────────────────
public sealed class WorkspaceStatusTests
{
    [Fact]
    public void From_ValidCode_ReturnsStatus()
        => Assert.Equal(WorkspaceStatus.Active, WorkspaceStatus.From("ACTIVE"));

    [Fact]
    public void From_InvalidCode_Throws()
        => Assert.Throws<ArgumentException>(() => WorkspaceStatus.From("INVALID"));

    [Theory]
    [InlineData("DRAFT",    "ACTIVE",   true)]
    [InlineData("DRAFT",    "ARCHIVED", true)]
    [InlineData("ACTIVE",   "CLOSED",   true)]
    [InlineData("ACTIVE",   "ARCHIVED", true)]
    [InlineData("CLOSED",   "ACTIVE",   true)]   // Re-open allowed
    [InlineData("CLOSED",   "ARCHIVED", true)]
    [InlineData("ARCHIVED", "DISPOSED", true)]
    [InlineData("DISPOSED", "ACTIVE",   false)]  // Terminal
    [InlineData("DISPOSED", "DRAFT",    false)]
    [InlineData("ACTIVE",   "DISPOSED", false)]  // Must archive first
    [InlineData("DRAFT",    "DISPOSED", false)]
    public void CanTransitionTo_Matrix(string from, string to, bool expected)
        => Assert.Equal(expected,
            WorkspaceStatus.From(from).CanTransitionTo(WorkspaceStatus.From(to)));

    [Fact]
    public void Draft_AllowsNewDocuments()
        => Assert.True(WorkspaceStatus.Draft.AllowsNewDocuments);

    [Fact]
    public void Active_AllowsNewDocuments()
        => Assert.True(WorkspaceStatus.Active.AllowsNewDocuments);

    [Fact]
    public void Closed_BlocksNewDocuments()
        => Assert.False(WorkspaceStatus.Closed.AllowsNewDocuments);

    [Fact]
    public void Archived_BlocksNewDocuments()
        => Assert.False(WorkspaceStatus.Archived.AllowsNewDocuments);

    [Fact]
    public void Disposed_IsTerminal()
        => Assert.True(WorkspaceStatus.Disposed.IsTerminal);
}

// ─── WORKSPACE ENTITY TESTS ───────────────────────────────────────────────────
public sealed class WorkspaceEntityTests
{
    private static Workspace MakeWorkspace()
        => Workspace.Create("مشروع اختبار", 1, 1, "WS-2026-00001", 1,
            classification: ClassificationLevel.Internal);

    // Factory
    [Fact]
    public void Create_SetsDefaultDraft()
        => Assert.Equal(WorkspaceStatus.Draft, MakeWorkspace().Status);

    [Fact]
    public void Create_RaisesCreatedEvent()
    {
        var ws = MakeWorkspace();
        Assert.Single(ws.DomainEvents);
        Assert.IsType<WorkspaceCreatedEvent>(ws.DomainEvents.First());
    }

    [Fact]
    public void Create_WorkspaceId_NotEmpty()
        => Assert.NotEqual(Guid.Empty, MakeWorkspace().WorkspaceId);

    [Fact]
    public void Create_IsBoundToExternal_False()
        => Assert.False(MakeWorkspace().IsBoundToExternal);

    // Lifecycle
    [Fact]
    public void Activate_FromDraft_Succeeds()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        Assert.Equal(WorkspaceStatus.Active, ws.Status);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceActivatedEvent);
    }

    [Fact]
    public void Activate_FromActive_Throws()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        Assert.Throws<InvalidOperationException>(() => ws.Activate(1));
    }

    [Fact]
    public void Close_FromActive_Succeeds()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        ws.Close(1, "Project completed");
        Assert.Equal(WorkspaceStatus.Closed, ws.Status);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceClosedEvent);
    }

    [Fact]
    public void Reopen_FromClosed_ToActive_Succeeds()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        ws.Close(1, null);
        ws.Activate(1); // Re-open
        Assert.Equal(WorkspaceStatus.Active, ws.Status);
    }

    [Fact]
    public void Archive_FromActive_Succeeds()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        ws.Archive(1);
        Assert.Equal(WorkspaceStatus.Archived, ws.Status);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceArchivedEvent);
    }

    [Fact]
    public void Archive_UnderLegalHold_Throws()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        ws.ApplyLegalHold(1);
        Assert.Throws<InvalidOperationException>(() => ws.Archive(1));
    }

    [Fact]
    public void Dispose_FromArchived_Succeeds()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        ws.Archive(1);
        ws.Dispose(1);
        Assert.Equal(WorkspaceStatus.Disposed, ws.Status);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceDisposedEvent);
    }

    [Fact]
    public void Dispose_UnderLegalHold_Throws()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        ws.Archive(1);
        ws.ApplyLegalHold(1);
        Assert.Throws<InvalidOperationException>(() => ws.Dispose(1));
    }

    [Fact]
    public void Dispose_FromActive_Throws()   // Must archive first
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        Assert.Throws<InvalidOperationException>(() => ws.Dispose(1));
    }

    // Legal hold
    [Fact]
    public void ApplyLegalHold_SetsFlag()
    {
        var ws = MakeWorkspace();
        ws.ApplyLegalHold(1);
        Assert.True(ws.IsLegalHold);
        Assert.NotNull(ws.LegalHoldAt);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceLegalHoldAppliedEvent);
    }

    [Fact]
    public void ApplyLegalHold_Twice_Throws()
    {
        var ws = MakeWorkspace();
        ws.ApplyLegalHold(1);
        Assert.Throws<InvalidOperationException>(() => ws.ApplyLegalHold(1));
    }

    [Fact]
    public void ReleaseLegalHold_ClearsFlag()
    {
        var ws = MakeWorkspace();
        ws.ApplyLegalHold(1);
        ws.ReleaseLegalHold(1);
        Assert.False(ws.IsLegalHold);
        Assert.Null(ws.LegalHoldAt);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceLegalHoldReleasedEvent);
    }

    [Fact]
    public void ReleaseLegalHold_WhenNotHeld_Throws()
    {
        var ws = MakeWorkspace();
        Assert.Throws<InvalidOperationException>(() => ws.ReleaseLegalHold(1));
    }

    // External binding
    [Fact]
    public void BindToExternalObject_SetsAllFields()
    {
        var ws = MakeWorkspace();
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement",
            "http://sap/wbs/001", "Project Alpha", 1);
        Assert.True(ws.IsBoundToExternal);
        Assert.Equal("SAP_PROD", ws.ExternalSystemCode);
        Assert.Equal("WBS-001", ws.ExternalObjectId);
        Assert.Equal("WBSElement", ws.ExternalObjectType);
        Assert.Equal("Pending", ws.SyncStatus);
        Assert.Equal(0, ws.SyncAttempts);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceLinkedToExternalEvent);
    }

    [Fact]
    public void BindToExternalObject_AlreadyBound_Throws()
    {
        var ws = MakeWorkspace();
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        Assert.Throws<InvalidOperationException>(() =>
            ws.BindToExternalObject("SF_CRM", "ACC-002", "Account", null, null, 1));
    }

    [Fact]
    public void UnbindExternalObject_ClearsBinding()
    {
        var ws = MakeWorkspace();
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        ws.UnbindExternalObject(1);
        Assert.False(ws.IsBoundToExternal);
        Assert.Null(ws.ExternalSystemCode);
        Assert.Null(ws.SyncStatus);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceExternalBindingRemovedEvent);
    }

    [Fact]
    public void UnbindExternalObject_WhenNotBound_Throws()
    {
        var ws = MakeWorkspace();
        Assert.Throws<InvalidOperationException>(() => ws.UnbindExternalObject(1));
    }

    // Sync state management
    [Fact]
    public void RecordSyncSuccess_SetsSyncedStatus()
    {
        var ws = MakeWorkspace();
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        ws.BeginSync(0);
        ws.RecordSyncSuccess(DateTime.UtcNow, 5, 0);
        Assert.Equal("Synced", ws.SyncStatus);
        Assert.NotNull(ws.LastSyncedAt);
        Assert.Null(ws.SyncError);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceSyncCompletedEvent);
    }

    [Fact]
    public void RecordSyncFailure_SetsFailedStatus()
    {
        var ws = MakeWorkspace();
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        ws.RecordSyncFailure("Connection timeout", 0);
        Assert.Equal("Failed", ws.SyncStatus);
        Assert.Equal("Connection timeout", ws.SyncError);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceSyncFailedEvent);
    }

    [Fact]
    public void BeginSync_IncrementsAttemptCount()
    {
        var ws = MakeWorkspace();
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        ws.BeginSync(0);
        Assert.Equal(1, ws.SyncAttempts);
        Assert.Equal("Syncing", ws.SyncStatus);
    }

    [Fact]
    public void BeginSync_WithoutBinding_Throws()
    {
        var ws = MakeWorkspace();
        Assert.Throws<InvalidOperationException>(() => ws.BeginSync(0));
    }

    // Document count
    [Fact]
    public void IncrementDecrement_DocumentCount()
    {
        var ws = MakeWorkspace();
        ws.IncrementDocumentCount(); ws.IncrementDocumentCount();
        Assert.Equal(2, ws.DocumentCount);
        ws.DecrementDocumentCount();
        Assert.Equal(1, ws.DocumentCount);
    }

    [Fact]
    public void DecrementDocumentCount_BelowZero_StaysAtZero()
    {
        var ws = MakeWorkspace();
        ws.DecrementDocumentCount();
        Assert.Equal(0, ws.DocumentCount);
    }

    // Classification governance
    [Fact]
    public void UpdateClassification_ToMoreRestrictive_RaisesEvent()
    {
        var ws = MakeWorkspace(); // Internal (2)
        ws.UpdateClassification(ClassificationLevel.Confidential, 1); // Confidential (3)
        Assert.Equal(ClassificationLevel.Confidential, ws.Classification);
        Assert.Contains(ws.DomainEvents, e => e is WorkspaceClassificationChangedEvent);
    }

    [Fact]
    public void UpdateClassification_ToLessRestrictive_NoEvent()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1,
            classification: ClassificationLevel.Confidential);
        ws.ClearDomainEvents(); // Clear creation event
        ws.UpdateClassification(ClassificationLevel.Internal, 1); // Less restrictive
        Assert.Equal(ClassificationLevel.Internal, ws.Classification);
        Assert.DoesNotContain(ws.DomainEvents, e => e is WorkspaceClassificationChangedEvent);
    }

    // Business rules
    [Fact]
    public void CanBeDeleted_DraftNoDocumentsNoHold_True()
    {
        var ws = MakeWorkspace();
        Assert.True(ws.CanBeDeleted());
    }

    [Fact]
    public void CanBeDeleted_ActiveStatus_False()
    {
        var ws = MakeWorkspace();
        ws.Activate(1);
        Assert.False(ws.CanBeDeleted());
    }

    [Fact]
    public void CanBeDeleted_HasDocuments_False()
    {
        var ws = MakeWorkspace();
        ws.IncrementDocumentCount();
        Assert.False(ws.CanBeDeleted());
    }

    [Fact]
    public void NeedsSync_Pending_True()
    {
        var ws = MakeWorkspace();
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        Assert.True(ws.NeedsSync());
    }

    [Fact]
    public void NeedsSync_TooManyAttempts_False()
    {
        var ws = MakeWorkspace();
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        for (int i = 0; i < 5; i++) ws.BeginSync(0);
        ws.RecordSyncFailure("Failed", 0);
        Assert.False(ws.NeedsSync()); // 5 attempts = dead-lettered
    }
}

// ─── WORKSPACE LIFECYCLE SERVICE TESTS ───────────────────────────────────────
public sealed class WorkspaceLifecycleServiceTests
{
    private readonly DocumentLifecycleService _docLifecycle = new();
    private readonly WorkspaceLifecycleService _lifecycle;

    public WorkspaceLifecycleServiceTests()
        => _lifecycle = new WorkspaceLifecycleService(_docLifecycle);

    private static Workspace MakeWorkspace(WorkspaceStatus? status = null)
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1);
        if (status == WorkspaceStatus.Active) ws.Activate(1);
        if (status == WorkspaceStatus.Closed) { ws.Activate(1); ws.Close(1, null); }
        if (status == WorkspaceStatus.Archived) { ws.Activate(1); ws.Archive(1); }
        return ws;
    }

    [Fact]
    public void ValidateArchive_NoLegalHold_Success()
    {
        var ws = MakeWorkspace(WorkspaceStatus.Active);
        var r = _lifecycle.ValidateArchive(ws);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void ValidateArchive_LegalHold_Fails()
    {
        var ws = MakeWorkspace(WorkspaceStatus.Active);
        ws.ApplyLegalHold(1);
        var r = _lifecycle.ValidateArchive(ws);
        Assert.False(r.IsSuccess);
        Assert.Contains("تجميد قانوني", r.Error);
    }

    [Fact]
    public void ValidateDispose_Archived_Success()
    {
        var ws = MakeWorkspace(WorkspaceStatus.Archived);
        var r = _lifecycle.ValidateDispose(ws);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void ValidateDispose_Active_Fails()
    {
        var ws = MakeWorkspace(WorkspaceStatus.Active);
        var r = _lifecycle.ValidateDispose(ws);
        Assert.False(r.IsSuccess);
        Assert.Contains("المؤرشفة", r.Error);
    }

    [Fact]
    public void ValidateDocumentBinding_ActiveWorkspace_Success()
    {
        var ws = MakeWorkspace(WorkspaceStatus.Active);
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        var r = _lifecycle.ValidateDocumentBinding(ws, doc);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void ValidateDocumentBinding_ClosedWorkspace_Fails()
    {
        var ws = MakeWorkspace(WorkspaceStatus.Closed);
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        var r = _lifecycle.ValidateDocumentBinding(ws, doc);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void ValidateDocumentBinding_LegalHold_Fails()
    {
        var ws = MakeWorkspace(WorkspaceStatus.Active);
        ws.ApplyLegalHold(1);
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        var r = _lifecycle.ValidateDocumentBinding(ws, doc);
        Assert.False(r.IsSuccess);
        Assert.Contains("تجميد قانوني", r.Error);
    }

    [Fact]
    public void GetDocumentsForCascadeArchive_PrimaryBindings_Returned()
    {
        var docId = Guid.NewGuid();
        var bindings = new[]
        {
            WorkspaceDocument.Create(Guid.NewGuid(), docId, 1, "Primary"),
            WorkspaceDocument.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Reference")
        };
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.TransitionStatus(DocumentStatus.Active, 1);

        var cascade = _lifecycle.GetDocumentsForCascadeArchive(bindings, new[] { doc });
        Assert.Single(cascade);
        Assert.Equal(docId, cascade.First().DocumentId);
    }

    [Fact]
    public void GetDocumentsForLegalHoldCascade_AllBindings_Returned()
    {
        var doc1Id = Guid.NewGuid();
        var doc2Id = Guid.NewGuid();
        var bindings = new[]
        {
            WorkspaceDocument.Create(Guid.NewGuid(), doc1Id, 1, "Primary"),
            WorkspaceDocument.Create(Guid.NewGuid(), doc2Id, 1, "Reference")
        };
        var doc1 = Document.Create("Test1", 1, 1, 1, "DOC-001");
        var doc2 = Document.Create("Test2", 1, 1, 1, "DOC-002");

        var cascade = _lifecycle.GetDocumentsForLegalHoldCascade(bindings, new[] { doc1, doc2 });
        Assert.Equal(2, cascade.Count());  // Both Primary and Reference
    }

    [Fact]
    public void ComputeGovernanceInheritance_WorkspaceMoreRestrictive_InheritsClassification()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1,
            classification: ClassificationLevel.Confidential);
        var doc = Document.Create("Doc", 1, 1, 1, "DOC-001"); // Internal by default

        var inheritance = _lifecycle.ComputeGovernanceInheritance(ws, doc);
        Assert.True(inheritance.InheritClassification);
        Assert.Equal(ClassificationLevel.Confidential, inheritance.NewClassification);
    }

    [Fact]
    public void ComputeGovernanceInheritance_WorkspaceLessRestrictive_NoClassificationInherit()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1,
            classification: ClassificationLevel.Public);
        var doc = Document.Create("Doc", 1, 1, 1, "DOC-001"); // Internal

        var inheritance = _lifecycle.ComputeGovernanceInheritance(ws, doc);
        Assert.False(inheritance.InheritClassification); // Doc is more restrictive — no downgrade
    }
}

// ─── WORKSPACE DOCUMENT BINDING TESTS ────────────────────────────────────────
public sealed class WorkspaceDocumentBindingTests
{
    [Fact]
    public void Create_PrimaryBinding_SetsBindingType()
    {
        var b = WorkspaceDocument.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Primary", "Test note");
        Assert.Equal("Primary", b.BindingType);
        Assert.True(b.IsActive);
        Assert.Equal("Test note", b.Note);
    }

    [Fact]
    public void Remove_SetsInactive()
    {
        var b = WorkspaceDocument.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Primary");
        b.Remove(1);
        Assert.False(b.IsActive);
    }

    [Fact]
    public void Workspace_ValidateCanBindDocument_Closed_Throws()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1);
        ws.Activate(1);
        ws.Close(1, null);
        Assert.Throws<InvalidOperationException>(() => ws.ValidateCanBindDocument());
    }

    [Fact]
    public void Workspace_ValidateCanBindDocument_Active_Succeeds()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1);
        ws.Activate(1);
        ws.ValidateCanBindDocument(); // Should not throw
    }
}

// ─── WORKSPACE TYPE TESTS ─────────────────────────────────────────────────────
public sealed class WorkspaceTypeTests
{
    [Fact]
    public void Create_CodeIsUppercase()
    {
        var wt = WorkspaceType.Create("project", "مشروع", "Project", 1, "WS-PROJ-");
        Assert.Equal("PROJECT", wt.TypeCode);
    }

    [Fact]
    public void LinkToExternalSystem_SetsFields()
    {
        var wt = WorkspaceType.Create("CONTRACT", "عقد", "Contract", 1);
        wt.LinkToExternalSystem("SAP_PROD", "Contract", required: true);
        Assert.Equal("SAP_PROD", wt.ExternalSystemCode);
        Assert.Equal("Contract", wt.ExternalObjectType);
        Assert.True(wt.RequireExternalBinding);
    }
}
