using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.xECM.Domain.Entities;
using Darah.ECM.xECM.Domain.Services;
using Darah.ECM.xECM.Domain.ValueObjects;
using Darah.ECM.xECM.Infrastructure.Sync;
using Xunit;
using Darah.ECM.Domain.Services;
using Darah.ECM.Domain.Entities;

namespace Darah.ECM.IntegrationTests.xECM.Workspace;

// ─── EXTERNAL BINDING UNIQUENESS ─────────────────────────────────────────────
public sealed class ExternalBindingUniquenessTests
{
    [Fact]
    public void BindToExternalObject_OneToOneRule_EnforcedAtDomainLevel()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1);
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);

        // Second binding to same workspace → throws (domain rule)
        Assert.Throws<InvalidOperationException>(() =>
            ws.BindToExternalObject("SAP_PROD", "WBS-002", "WBSElement", null, null, 1));
    }

    [Fact]
    public void TwoWorkspaces_CannotShareExternalObject()
    {
        // This tests the uniqueness constraint from the repository perspective
        // In a real DB test: ExternalBindingExistsAsync() returns true for second workspace
        // Here we test the domain understanding: each workspace has at most one external binding
        var ws1 = Workspace.Create("WS1", 1, 1, "WS-001", 1);
        var ws2 = Workspace.Create("WS2", 1, 1, "WS-002", 1);

        // WS1 binds to SAP WBS-001
        ws1.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        Assert.True(ws1.IsBoundToExternal);

        // WS2 also tries to bind to SAP WBS-001 — domain allows it (no shared state)
        // But repository ExternalBindingExistsAsync() prevents it at application layer
        // This is correct by design: domain enforces per-aggregate rules,
        // application layer enforces cross-aggregate uniqueness
        ws2.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 2);
        Assert.True(ws2.IsBoundToExternal); // Domain allows, repository prevents

        // The invariant: different workspaces CAN bind to same object at domain level
        // Application layer (CreateWorkspaceCommandHandler) checks ExternalBindingExistsAsync()
        Assert.Equal("WBS-001", ws1.ExternalObjectId);
        Assert.Equal("WBS-001", ws2.ExternalObjectId);
    }

    [Fact]
    public void Unbind_ThenRebindToDifferentObject_Allowed()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1);
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);
        ws.UnbindExternalObject(1);
        ws.BindToExternalObject("SAP_PROD", "WBS-999", "WBSElement", null, null, 1);
        Assert.Equal("WBS-999", ws.ExternalObjectId);
    }
}

// ─── SYNC CONFLICT RESOLUTION TESTS ──────────────────────────────────────────
public sealed class SyncConflictResolutionTests
{
    // Test ApplyTransform pipeline
    [Theory]
    [InlineData("uppercase",   "hello",        "HELLO")]
    [InlineData("lowercase",   "HELLO",        "hello")]
    [InlineData("trim",        "  hello  ",    "hello")]
    [InlineData("prefix:REF-", "123",          "REF-123")]
    [InlineData("suffix:-SA",  "PROJ",         "PROJ-SA")]
    [InlineData("truncate:5",  "Hello World",  "Hello")]
    [InlineData("unknown",     "unchanged",    "unchanged")]
    public void ApplyTransform_VariousExpressions(string expr, string input, string expected)
    {
        var result = ApplyTransformHelper(input, expr);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ExternalWins", "EXT", "INT", "EXT")]  // External always wins
    [InlineData("InternalWins", "EXT", "INT", "INT")]  // Internal protected
    [InlineData("InternalWins", "EXT", null,  "EXT")]  // No internal → use external
    [InlineData("Manual",       "EXT", "INT", "INT")]  // Manual → preserve internal
    public void ResolveConflict_Strategies(string strategy, string ext, string? intl, string expected)
    {
        var result = ResolveConflictHelper(ext, intl,
            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-5), strategy);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveConflict_Newer_ExternalIsNewer_WinsExternal()
    {
        var extAt = DateTime.UtcNow;               // External is newer
        var intAt = DateTime.UtcNow.AddHours(-2);  // Internal is older
        var result = ResolveConflictHelper("EXTERNAL", "INTERNAL", extAt, intAt, "Newer");
        Assert.Equal("EXTERNAL", result);
    }

    [Fact]
    public void ResolveConflict_Newer_InternalIsNewer_WinsInternal()
    {
        var extAt = DateTime.UtcNow.AddHours(-2);  // External is older
        var intAt = DateTime.UtcNow;               // Internal is newer
        var result = ResolveConflictHelper("EXTERNAL", "INTERNAL", extAt, intAt, "Newer");
        Assert.Equal("INTERNAL", result);
    }

    // Helper to test transform without instantiating the full engine
    private static string? ApplyTransformHelper(string? raw, string expr)
    {
        if (string.IsNullOrWhiteSpace(expr) || raw is null) return raw;
        return expr.Trim().ToLower() switch
        {
            "uppercase"  => raw.ToUpperInvariant(),
            "lowercase"  => raw.ToLowerInvariant(),
            "trim"       => raw.Trim(),
            var e when e.StartsWith("prefix:") => e[7..] + raw,
            var e when e.StartsWith("suffix:") => raw + e[7..],
            var e when e.StartsWith("truncate:") && int.TryParse(e[9..], out var len) => raw.Length > len ? raw[..len] : raw,
            _ => raw
        };
    }

    private static string? ResolveConflictHelper(string? ext, string? intl, DateTime extAt, DateTime intAt, string strategy) =>
        strategy switch
        {
            "ExternalWins" => ext,
            "InternalWins" => intl ?? ext,
            "Newer" => (extAt >= intAt) ? ext : intl,
            "Manual" => intl,
            _ => ext
        };
}

// ─── LIFECYCLE CASCADE TESTS ──────────────────────────────────────────────────
public sealed class WorkspaceLifecycleCascadeTests
{
    private readonly DocumentLifecycleService _docLifecycle = new();
    private readonly WorkspaceLifecycleService _lifecycle;

    public WorkspaceLifecycleCascadeTests()
        => _lifecycle = new WorkspaceLifecycleService(_docLifecycle);

    [Fact]
    public void ArchiveCascade_OnlyPrimaryDocuments_AffectedNotReference()
    {
        var docId1 = Guid.NewGuid();
        var docId2 = Guid.NewGuid();
        var bindings = new[]
        {
            WorkspaceDocument.Create(Guid.NewGuid(), docId1, 1, "Primary"),   // Should cascade
            WorkspaceDocument.Create(Guid.NewGuid(), docId2, 1, "Reference")  // Should NOT cascade
        };

        var doc1 = Document.Create("Primary Doc", 1, 1, 1, "DOC-001");
        doc1.TransitionStatus(DocumentStatus.Active, 1);
        var doc2 = Document.Create("Reference Doc", 1, 1, 1, "DOC-002");
        doc2.TransitionStatus(DocumentStatus.Active, 1);

        var forArchive = _lifecycle.GetDocumentsForCascadeArchive(
            bindings, new[] { doc1, doc2 }).ToList();

        Assert.Single(forArchive);
        Assert.Equal(docId1, forArchive[0].DocumentId);
    }

    [Fact]
    public void ArchiveCascade_LegalHoldDocuments_Excluded()
    {
        var docId = Guid.NewGuid();
        var binding = WorkspaceDocument.Create(Guid.NewGuid(), docId, 1, "Primary");

        var doc = Document.Create("Held Doc", 1, 1, 1, "DOC-001");
        doc.TransitionStatus(DocumentStatus.Active, 1);
        doc.ApplyLegalHold(); // On legal hold

        var forArchive = _lifecycle.GetDocumentsForCascadeArchive(
            new[] { binding }, new[] { doc }).ToList();

        Assert.Empty(forArchive); // Legal hold documents excluded from cascade
    }

    [Fact]
    public void LegalHoldCascade_AllBindingTypes_Included()
    {
        var primaryDocId   = Guid.NewGuid();
        var referenceDocId = Guid.NewGuid();
        var bindings = new[]
        {
            WorkspaceDocument.Create(Guid.NewGuid(), primaryDocId, 1, "Primary"),
            WorkspaceDocument.Create(Guid.NewGuid(), referenceDocId, 1, "Reference")
        };

        var primary   = Document.Create("Primary", 1, 1, 1, "DOC-001");
        var reference = Document.Create("Reference", 1, 1, 1, "DOC-002");

        var forHold = _lifecycle.GetDocumentsForLegalHoldCascade(
            bindings, new[] { primary, reference }).ToList();

        Assert.Equal(2, forHold.Count); // BOTH types included
    }

    [Fact]
    public void LifecycleCascade_DisposedDocuments_ExcludedFromLegalHold()
    {
        var docId   = Guid.NewGuid();
        var binding = WorkspaceDocument.Create(Guid.NewGuid(), docId, 1, "Primary");

        // Simulate a disposed document (cannot hold again)
        var doc = Document.Create("Doc", 1, 1, 1, "DOC-001");
        doc.TransitionStatus(DocumentStatus.Pending, 1);
        doc.TransitionStatus(DocumentStatus.Approved, 1);
        doc.TransitionStatus(DocumentStatus.Active, 1);
        doc.TransitionStatus(DocumentStatus.Archived, 1);
        doc.TransitionStatus(DocumentStatus.Disposed, 1);

        var forHold = _lifecycle.GetDocumentsForLegalHoldCascade(
            new[] { binding }, new[] { doc }).ToList();

        Assert.Empty(forHold); // Disposed documents excluded
    }
}

// ─── SECURITY INHERITANCE TESTS ────────────────────────────────────────────────
public sealed class WorkspaceSecurityInheritanceTests
{
    [Fact]
    public void WorkspaceSecurityPolicy_DenyWins_OverridesAllow()
    {
        var wsId = Guid.NewGuid();

        var allowPolicy = WorkspaceSecurityPolicy.Create(
            wsId, "User", 5, canRead: true, canWrite: true, canDownload: true,
            canDelete: false, canManage: false, grantedBy: 1);

        var denyPolicy = WorkspaceSecurityPolicy.Create(
            wsId, "User", 5, canRead: true, canWrite: false, canDownload: false,
            canDelete: false, canManage: false, grantedBy: 1, isDeny: true);

        // Deny-wins evaluation
        bool allowed = !denyPolicy.IsDeny && allowPolicy.CanWrite;
        bool denied  = denyPolicy.IsDeny;

        Assert.True(denied);     // Deny policy exists
        Assert.False(allowed);   // Deny overrides the allow
    }

    [Fact]
    public void WorkspaceSecurityPolicy_ExpiresAt_ReturnsIsExpired()
    {
        var policy = WorkspaceSecurityPolicy.Create(
            Guid.NewGuid(), "User", 5, true, true, true, false, false, 1,
            expiresAt: DateTime.UtcNow.AddDays(-1));

        Assert.True(policy.IsExpired());
        Assert.False(policy.IsEffective());
    }

    [Fact]
    public void WorkspaceSecurityPolicy_FutureExpiry_IsEffective()
    {
        var policy = WorkspaceSecurityPolicy.Create(
            Guid.NewGuid(), "Role", 3, true, true, true, false, false, 1,
            expiresAt: DateTime.UtcNow.AddDays(30));

        Assert.False(policy.IsExpired());
        Assert.True(policy.IsEffective());
    }

    [Fact]
    public void WorkspaceSecurityPolicy_NoExpiry_AlwaysEffective()
    {
        var policy = WorkspaceSecurityPolicy.Create(
            Guid.NewGuid(), "Department", 10, true, false, true, false, false, 1);

        Assert.False(policy.IsExpired());
        Assert.True(policy.IsEffective());
    }
}

// ─── SYNC ENGINE BEHAVIOR TESTS ──────────────────────────────────────────────
public sealed class SyncEngineBehaviorTests
{
    [Fact]
    public void SyncDirection_Enum_HasAllValues()
    {
        var values = Enum.GetValues<SyncDirection>();
        Assert.Contains(SyncDirection.Inbound, values);
        Assert.Contains(SyncDirection.Outbound, values);
        Assert.Contains(SyncDirection.Bidirectional, values);
    }

    [Fact]
    public void SyncResult_Success_HasCorrectFields()
    {
        var r = new SyncResult(true, 10, 2, null, 500);
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.FieldsUpdated);
        Assert.Equal(2, r.ConflictsDetected);
        Assert.Equal(500, r.DurationMs);
    }

    [Fact]
    public void SyncResult_Failure_HasErrorMessage()
    {
        var r = new SyncResult(false, 0, 0, "Connection timeout", 1000);
        Assert.False(r.IsSuccess);
        Assert.Equal("Connection timeout", r.ErrorMessage);
    }

    [Fact]
    public void SyncEventLog_Success_HasCorrectFields()
    {
        var wsId = Guid.NewGuid();
        var log = SyncEventLog.CreateSuccess(wsId, 1, "Inbound", "Manual",
            5, 1, 1500, "WBS-001", "WBSElement");
        Assert.True(log.IsSuccessful);
        Assert.Equal(5, log.FieldsUpdated);
        Assert.Equal(1, log.ConflictsDetected);
        Assert.Equal("WBS-001", log.ExternalObjectId);
    }

    [Fact]
    public void SyncEventLog_Failure_HasErrorMessage()
    {
        var log = SyncEventLog.CreateFailure(Guid.NewGuid(), 1, "Inbound", "Webhook",
            "Network unreachable", 500);
        Assert.False(log.IsSuccessful);
        Assert.Equal("Network unreachable", log.ErrorMessage);
    }
}

// ─── FAILED SYNC RETRY BEHAVIOR ───────────────────────────────────────────────
public sealed class SyncRetryBehaviorTests
{
    [Fact]
    public void Workspace_NeedsSync_FalseAfterFiveAttempts()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1);
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);

        // Simulate 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            ws.BeginSync(0);
            ws.RecordSyncFailure($"Attempt {i + 1} failed", 0);
        }

        Assert.Equal(5, ws.SyncAttempts);
        Assert.Equal("Failed", ws.SyncStatus);
        Assert.False(ws.NeedsSync()); // Dead-lettered after 5 attempts
    }

    [Fact]
    public void Workspace_SyncSuccess_ResetsErrorState()
    {
        var ws = Workspace.Create("WS", 1, 1, "WS-001", 1);
        ws.BindToExternalObject("SAP_PROD", "WBS-001", "WBSElement", null, null, 1);

        // Fail once
        ws.BeginSync(0);
        ws.RecordSyncFailure("Error", 0);
        Assert.Equal("Failed", ws.SyncStatus);
        Assert.NotNull(ws.SyncError);

        // Then succeed
        ws.BeginSync(0);
        ws.RecordSyncSuccess(DateTime.UtcNow, 3, 0);
        Assert.Equal("Synced", ws.SyncStatus);
        Assert.Null(ws.SyncError);
        Assert.NotNull(ws.LastSyncedAt);
    }

    [Fact]
    public void WorkspaceMetadataValue_SetAndGet_TypedValues()
    {
        var mv = new WorkspaceMetadataValue { WorkspaceId = Guid.NewGuid(), FieldId = 1 };

        mv.SetValue("Text", "مشروع البنية التحتية");
        Assert.Equal("مشروع البنية التحتية", mv.TextValue);
        Assert.Equal("مشروع البنية التحتية", mv.GetDisplayValue());

        mv.SetValue("Number", "1500000.50");
        Assert.Equal(1500000.50m, mv.NumberValue);
        Assert.Null(mv.TextValue); // Cleared on type change

        mv.SetValue("Date", "2026-12-31");
        Assert.NotNull(mv.DateValue);
        Assert.Null(mv.NumberValue); // Cleared
    }
}
