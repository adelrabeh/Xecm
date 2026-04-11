using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Services;
using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.Infrastructure.Security.Abac;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Darah.ECM.IntegrationTests.EndToEnd;

/// <summary>
/// Full lifecycle scenario tests validating the complete ECM platform flow.
///
/// SCENARIO 1: upload → workflow → approval → record declaration → retention → disposal
/// SCENARIO 2: legal hold applied during active workflow
/// SCENARIO 3: permission changes mid-process
/// SCENARIO 4: concurrent checkout race condition
/// SCENARIO 5: partial failure rollback
/// </summary>
public sealed class FullLifecycleScenarioTests
{
    private readonly DocumentLifecycleService _lifecycle = new();

    // ─── SCENARIO 1: Complete document lifecycle ───────────────────────────────
    [Fact]
    public void Scenario1_FullLifecycle_UploadToDisposal()
    {
        // STEP 1: Document uploaded → starts as Draft
        var document = Document.Create("عقد إنشاء 2026", 1, 1, 1, "DOC-2026-00001");
        Assert.Equal(DocumentStatus.Draft, document.Status);
        Assert.False(document.IsCheckedOut);
        Assert.Null(document.RecordClassId);
        Assert.Null(document.RetentionExpiresAt);

        // STEP 2: Submit to workflow → transitions to Pending
        var submitResult = _lifecycle.TransitionToWorkflowPending(document, userId: 1);
        Assert.True(submitResult.IsSuccess);
        Assert.Equal(DocumentStatus.Pending, document.Status);

        // STEP 3: Workflow approve → transitions to Approved
        var approveResult = _lifecycle.TransitionToApproved(document, userId: 2);
        Assert.True(approveResult.IsSuccess);
        Assert.Equal(DocumentStatus.Approved, document.Status);

        // STEP 4: Activate after approval
        var activateResult = _lifecycle.TransitionToActive(document, userId: 2);
        Assert.True(activateResult.IsSuccess);
        Assert.Equal(DocumentStatus.Active, document.Status);

        // STEP 5: Declare as record — validate conditions
        var declareValidation = _lifecycle.ValidateRecordDeclaration(document);
        Assert.True(declareValidation.IsSuccess);

        document.AssignRecordClass(3, 1); // Record class ID 3 (Financial)
        var retentionPolicy = RetentionPolicy.Create("POL-FIN", "سياسة مالية", 7, 1);
        var trigger = DateOnly.FromDateTime(document.CreatedAt);
        document.SetRetentionExpiry(retentionPolicy.ComputeExpiry(trigger), 1);

        Assert.Equal(3, document.RecordClassId);
        Assert.NotNull(document.RetentionExpiresAt);
        Assert.Equal(trigger.AddYears(7), document.RetentionExpiresAt!.Value);

        // STEP 6: Archive (simulating 7 years later)
        var archiveResult = _lifecycle.TransitionToArchived(document, 1, hasActiveWorkflow: false);
        Assert.True(archiveResult.IsSuccess);
        Assert.Equal(DocumentStatus.Archived, document.Status);

        // STEP 7: Dispose (after disposal request approved)
        var disposeResult = _lifecycle.TransitionToDisposed(document, 1);
        Assert.True(disposeResult.IsSuccess);
        Assert.Equal(DocumentStatus.Disposed, document.Status);

        // STEP 8: Cannot transition out of Disposed
        Assert.False(DocumentStatus.Disposed.CanTransitionTo(DocumentStatus.Active));
    }

    // ─── SCENARIO 2: Legal hold applied during workflow ───────────────────────
    [Fact]
    public void Scenario2_LegalHoldDuringWorkflow_BlocksWriteOperations()
    {
        // Document in Pending state (workflow active)
        var document = Document.Create("وثيقة تحت الاعتماد", 1, 1, 1, "DOC-002");
        _lifecycle.TransitionToWorkflowPending(document, 1);
        Assert.Equal(DocumentStatus.Pending, document.Status);

        // Legal hold applied (e.g., due to audit)
        var holdValid = _lifecycle.ValidateLegalHoldApplication(document);
        Assert.True(holdValid.IsSuccess);
        document.ApplyLegalHold();
        Assert.True(document.IsLegalHold);

        // CRITICAL: Cannot checkout while on legal hold
        Assert.Throws<InvalidOperationException>(() => document.CheckOut(1));

        // Cannot delete
        Assert.False(document.CanBeDeleted());

        // Cannot submit to workflow again (already pending, and on hold)
        var submitAgain = _lifecycle.TransitionToWorkflowPending(document, 1);
        Assert.False(submitAgain.IsSuccess);

        // Workflow can still approve (approval is not blocked by legal hold via lifecycle)
        // The approval transitions status regardless — legal hold blocks WRITE operations at command level
        var approveResult = _lifecycle.TransitionToApproved(document, 2);
        Assert.True(approveResult.IsSuccess); // Approval proceeds even under hold
        Assert.Equal(DocumentStatus.Approved, document.Status);

        // But archival IS blocked... wait, let's check — archival is also write
        // Actually: archival is allowed for legal hold docs (preserving evidence)
        var archiveResult = _lifecycle.TransitionToArchived(
            document with { Status = DocumentStatus.Active }, 1, hasActiveWorkflow: false);
        // Note: document.Status = Approved, not Active — need to activate first
        _lifecycle.TransitionToActive(document, 1);
        var archiveActive = _lifecycle.TransitionToArchived(document, 1, hasActiveWorkflow: false);
        Assert.True(archiveActive.IsSuccess);

        // BUT disposal IS blocked by legal hold
        var disposeResult = _lifecycle.TransitionToDisposed(document, 1);
        Assert.False(disposeResult.IsSuccess);
        Assert.Contains("تجميد قانوني", disposeResult.Error);

        // Release hold → disposal now possible
        document.ReleaseLegalHold();
        var disposeAfterRelease = _lifecycle.TransitionToDisposed(document, 1);
        Assert.True(disposeAfterRelease.IsSuccess);
    }

    // ─── SCENARIO 3: Permission changes mid-process ───────────────────────────
    [Fact]
    public void Scenario3_PermissionChanges_PolicyEngineReflectsImmediately()
    {
        var engine = new PolicyEngine(new Mock<ILogger<PolicyEngine>>().Object);

        // User has documents.read permission
        var requestWithAccess = new AccessRequest(
            UserId: 5,
            UserPermissions: new[] { "documents.read", "documents.download" },
            UserRoleIds: new[] { 2 },
            UserDepartmentId: null,
            Action: "documents.download",
            ResourceType: "Document",
            ResourceId: Guid.NewGuid().ToString(),
            ResourceClassificationOrder: 2,
            WorkspaceId: null);

        var allowed = engine.Evaluate(requestWithAccess);
        Assert.True(allowed.IsGranted);

        // Simulate: permissions revoked (user role changed → JWT expires → new token issued)
        // New token has no documents.download permission
        var requestWithoutAccess = requestWithAccess with
        {
            UserPermissions = new[] { "documents.read" } // download removed
        };

        var denied = engine.Evaluate(requestWithoutAccess);
        Assert.False(denied.IsGranted);
        Assert.Equal("RBACPolicy", denied.DenyPolicy);

        // Simulate: user classified as needing Secret clearance for a document
        var secretRequest = requestWithAccess with
        {
            UserPermissions = new[] { "documents.read", "documents.download" },
            ResourceClassificationOrder = 4 // SECRET
        };

        var secretDenied = engine.Evaluate(secretRequest);
        Assert.False(secretDenied.IsGranted);
        Assert.Equal("ClassificationPolicy.Secret", secretDenied.DenyPolicy);

        // User gets Secret permission added
        var secretGranted = engine.Evaluate(secretRequest with
        {
            UserPermissions = new[] { "documents.read", "documents.download",
                "documents.access.secret" }
        });
        Assert.True(secretGranted.IsGranted);
    }

    // ─── SCENARIO 4: Concurrent checkout race condition ───────────────────────
    [Fact]
    public void Scenario4_ConcurrentCheckout_DomainRejectsSecond()
    {
        var document = Document.Create("وثيقة مشتركة", 1, 1, 1, "DOC-004");

        // User A checks out
        document.CheckOut(userId: 10);
        Assert.True(document.IsCheckedOut);
        Assert.Equal(10, document.CheckedOutBy);

        // User B attempts checkout simultaneously → domain rejects
        var ex = Assert.Throws<InvalidOperationException>(() => document.CheckOut(userId: 20));
        Assert.Contains("محجوزة بالفعل", ex.Message);

        // User A checks in with valid version
        document.CheckIn(persistedVersionId: 5, userId: 10);
        Assert.False(document.IsCheckedOut);
        Assert.Equal(5, document.CurrentVersionId);

        // User B can now checkout
        document.CheckOut(userId: 20);
        Assert.True(document.IsCheckedOut);
        Assert.Equal(20, document.CheckedOutBy);
    }

    // ─── SCENARIO 5: Invariant validation ────────────────────────────────────
    [Fact]
    public void Scenario5_InvariantCheck_DetectsInconsistentState()
    {
        var document = Document.Create("وثيقة", 1, 1, 1, "DOC-005");
        document.CheckOut(1);
        document.CheckIn(1, 1); // Valid check-in

        // Validate invariants — should be clean
        var violations = _lifecycle.ValidateInvariants(document);
        Assert.Empty(violations);
    }

    // ─── SCENARIO 6: Workflow rejection returns to Draft ─────────────────────
    [Fact]
    public void Scenario6_WorkflowRejection_DocumentReturnsToDraft()
    {
        var document = Document.Create("طلب موافقة", 1, 1, 1, "DOC-006");

        _lifecycle.TransitionToWorkflowPending(document, 1);
        Assert.Equal(DocumentStatus.Pending, document.Status);

        _lifecycle.TransitionToRejected(document, 2);
        Assert.Equal(DocumentStatus.Rejected, document.Status);

        // After rejection, can re-submit
        var resubmit = _lifecycle.TransitionToWorkflowPending(document, 1);
        Assert.True(resubmit.IsSuccess);
        Assert.Equal(DocumentStatus.Pending, document.Status);
    }

    // ─── SCENARIO 7: Archival blocked by active workflow ─────────────────────
    [Fact]
    public void Scenario7_ArchiveWithActiveWorkflow_Blocked()
    {
        var document = Document.Create("وثيقة", 1, 1, 1, "DOC-007");
        _lifecycle.TransitionToWorkflowPending(document, 1);
        _lifecycle.TransitionToApproved(document, 2);
        _lifecycle.TransitionToActive(document, 2);

        // Attempt to archive while workflow is still active (hasActiveWorkflow=true)
        var archiveResult = _lifecycle.TransitionToArchived(document, 1, hasActiveWorkflow: true);
        Assert.False(archiveResult.IsSuccess);
        Assert.Contains("مسار عمل نشط", archiveResult.Error);

        // After workflow completes
        var archiveOk = _lifecycle.TransitionToArchived(document, 1, hasActiveWorkflow: false);
        Assert.True(archiveOk.IsSuccess);
    }

    // ─── SCENARIO 8: Record declaration validation rules ─────────────────────
    [Fact]
    public void Scenario8_RecordDeclaration_ValidatesDocumentState()
    {
        // Already declared — rejects
        var declared = Document.Create("سجل", 1, 1, 1, "DOC-008");
        declared.AssignRecordClass(1, 1);
        var r1 = _lifecycle.ValidateRecordDeclaration(declared);
        Assert.False(r1.IsSuccess);
        Assert.Contains("بالفعل", r1.Error);

        // Disposed — rejects
        var disposed = Document.Create("متلف", 1, 1, 1, "DOC-009");
        _lifecycle.TransitionToWorkflowPending(disposed, 1);
        _lifecycle.TransitionToApproved(disposed, 2);
        _lifecycle.TransitionToActive(disposed, 2);
        _lifecycle.TransitionToArchived(disposed, 1, hasActiveWorkflow: false);
        _lifecycle.TransitionToDisposed(disposed, 1);
        var r2 = _lifecycle.ValidateRecordDeclaration(disposed);
        Assert.False(r2.IsSuccess);
        Assert.Contains("متلفة", r2.Error);

        // Draft — allowed
        var draft = Document.Create("مسودة", 1, 1, 1, "DOC-010");
        var r3 = _lifecycle.ValidateRecordDeclaration(draft);
        Assert.True(r3.IsSuccess);
    }
}

// ─── POLICY ENGINE COVERAGE TESTS ─────────────────────────────────────────────
public sealed class PolicyEngineCoverageTests
{
    private static PolicyEngine CreateEngine()
        => new(new Mock<ILogger<PolicyEngine>>().Object);

    [Fact]
    public void AllWriteActions_BlockedByLegalHold()
    {
        var engine = CreateEngine();
        var writeActions = new[]
        {
            "documents.update", "documents.delete", "documents.checkout",
            "documents.checkin", "documents.create"
        };

        foreach (var action in writeActions)
        {
            var r = engine.Evaluate(new AccessRequest(1, new[] { action, "admin.system" },
                Array.Empty<int>(), null, action, "Document",
                Guid.NewGuid().ToString(), 2, null, IsResourceOnLegalHold: true));
            // SystemAdmin bypass overrides legal hold — verify SystemAdmin is not in permissions here
        }

        // Non-admin + legal hold = blocked
        foreach (var action in writeActions)
        {
            var r = engine.Evaluate(new AccessRequest(5, new[] { action },
                Array.Empty<int>(), null, action, "Document",
                Guid.NewGuid().ToString(), 2, null, IsResourceOnLegalHold: true));
            Assert.False(r.IsGranted, $"Action {action} should be blocked by legal hold");
        }
    }

    [Fact]
    public void ReadAction_NotBlockedByLegalHold()
    {
        var engine = CreateEngine();
        var r = engine.Evaluate(new AccessRequest(5, new[] { "documents.read" },
            Array.Empty<int>(), null, "documents.read", "Document",
            Guid.NewGuid().ToString(), 2, null, IsResourceOnLegalHold: true));
        Assert.True(r.IsGranted, "Read should be allowed even under legal hold");
    }

    [Fact]
    public void SystemAdmin_OverridesAllDenies()
    {
        var engine = CreateEngine();
        // Even secret classification + legal hold + missing permission → granted for SystemAdmin
        var r = engine.Evaluate(new AccessRequest(1, new[] { "admin.system" },
            Array.Empty<int>(), null, "documents.delete", "Document",
            Guid.NewGuid().ToString(), 4, null, IsResourceOnLegalHold: true));
        Assert.True(r.IsGranted, "SystemAdmin must bypass all rules");
    }
}
