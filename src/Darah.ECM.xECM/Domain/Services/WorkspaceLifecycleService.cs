using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Services;
using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.xECM.Domain.Entities;
using Darah.ECM.xECM.Domain.ValueObjects;

namespace Darah.ECM.xECM.Domain.Services;

/// <summary>
/// Workspace lifecycle service — governs workspace status transitions
/// and their CASCADE effects on bound documents.
///
/// GOVERNANCE CASCADE RULES:
///   Workspace CLOSED:
///     - No new documents can be added
///     - Existing documents NOT affected (they continue their own lifecycle)
///
///   Workspace ARCHIVED:
///     - All Primary-bound documents → transition to Archived (if Active or Approved)
///     - Reference-bound documents → NOT affected
///
///   Workspace DISPOSED:
///     - All Primary-bound documents flagged for disposal review
///     - Creates DisposalRequest automatically
///
///   Workspace LEGAL HOLD:
///     - All bound documents (Primary + Reference) → ApplyLegalHold()
///     - Lock prevents workspace archival/disposal until hold released
///
///   Workspace CLASSIFICATION UPGRADED:
///     - All bound documents' classification upgraded if workspace is MORE restrictive
///     - Documents cannot be DOWNGRADED via workspace — only upgraded
///
///   Workspace RETENTION POLICY SET:
///     - Bound documents without explicit retention → inherit workspace policy
/// </summary>
public sealed class WorkspaceLifecycleService
{
    private readonly DocumentLifecycleService _docLifecycle;

    public WorkspaceLifecycleService(DocumentLifecycleService docLifecycle)
        => _docLifecycle = docLifecycle;

    // ─── Validation ───────────────────────────────────────────────────────────

    public Result ValidateActivate(Workspace workspace)
    {
        if (workspace.Status != WorkspaceStatus.Draft && workspace.Status != WorkspaceStatus.Closed)
            return Result.Fail($"لا يمكن تفعيل مساحة عمل بحالة '{workspace.Status}'");
        return Result.Ok();
    }

    public Result ValidateClose(Workspace workspace)
    {
        if (workspace.Status == WorkspaceStatus.Disposed)
            return Result.Fail("مساحة العمل متلفة");
        if (!workspace.Status.CanTransitionTo(WorkspaceStatus.Closed))
            return Result.Fail($"الانتقال من '{workspace.Status}' إلى Closed غير مسموح");
        return Result.Ok();
    }

    public Result ValidateArchive(Workspace workspace)
    {
        if (workspace.IsLegalHold)
            return Result.Fail("لا يمكن أرشفة مساحة عمل خاضعة لتجميد قانوني");
        if (!workspace.Status.CanTransitionTo(WorkspaceStatus.Archived))
            return Result.Fail($"الانتقال من '{workspace.Status}' إلى Archived غير مسموح");
        return Result.Ok();
    }

    public Result ValidateDispose(Workspace workspace)
    {
        if (workspace.IsLegalHold)
            return Result.Fail("لا يمكن إتلاف مساحة عمل خاضعة لتجميد قانوني");
        if (workspace.Status != WorkspaceStatus.Archived)
            return Result.Fail("يمكن إتلاف مساحات العمل المؤرشفة فقط");
        return Result.Ok();
    }

    public Result ValidateLegalHold(Workspace workspace)
    {
        if (workspace.Status == WorkspaceStatus.Disposed)
            return Result.Fail("لا يمكن تطبيق التجميد على مساحة عمل متلفة");
        if (workspace.IsLegalHold)
            return Result.Fail("التجميد القانوني مطبق على مساحة العمل بالفعل");
        return Result.Ok();
    }

    public Result ValidateDocumentBinding(Workspace workspace, Document document)
    {
        if (!workspace.Status.AllowsNewDocuments)
            return Result.Fail($"لا يمكن إضافة وثائق لمساحة عمل بحالة '{workspace.Status}'");
        if (workspace.IsLegalHold)
            return Result.Fail("لا يمكن إضافة وثائق لمساحة عمل خاضعة لتجميد قانوني");
        if (document.IsDeleted)
            return Result.Fail("الوثيقة محذوفة");
        return Result.Ok();
    }

    // ─── Cascade rules ────────────────────────────────────────────────────────

    /// <summary>
    /// Determines which documents should be archived when a workspace is archived.
    /// Returns only Primary-bound documents that CAN be archived.
    /// </summary>
    public IEnumerable<Document> GetDocumentsForCascadeArchive(
        IEnumerable<WorkspaceDocument> bindings,
        IEnumerable<Document> documents)
    {
        var primaryDocIds = bindings
            .Where(b => b.BindingType == "Primary" && b.IsActive)
            .Select(b => b.DocumentId)
            .ToHashSet();

        return documents.Where(d =>
            primaryDocIds.Contains(d.DocumentId)
            && !d.IsLegalHold
            && (d.Status == DocumentStatus.Active || d.Status == DocumentStatus.Approved));
    }

    /// <summary>
    /// Determines which documents should be flagged for disposal when a workspace is disposed.
    /// Legal hold documents are excluded (they cannot be disposed).
    /// </summary>
    public IEnumerable<Document> GetDocumentsForCascadeDispose(
        IEnumerable<WorkspaceDocument> bindings,
        IEnumerable<Document> documents)
    {
        var primaryDocIds = bindings
            .Where(b => b.BindingType == "Primary" && b.IsActive)
            .Select(b => b.DocumentId)
            .ToHashSet();

        return documents.Where(d =>
            primaryDocIds.Contains(d.DocumentId)
            && !d.IsLegalHold
            && d.Status == DocumentStatus.Archived);
    }

    /// <summary>
    /// All bound documents (Primary AND Reference) get legal hold applied.
    /// </summary>
    public IEnumerable<Document> GetDocumentsForLegalHoldCascade(
        IEnumerable<WorkspaceDocument> bindings,
        IEnumerable<Document> documents)
    {
        var boundDocIds = bindings
            .Where(b => b.IsActive)
            .Select(b => b.DocumentId)
            .ToHashSet();

        return documents.Where(d =>
            boundDocIds.Contains(d.DocumentId)
            && !d.IsLegalHold
            && d.Status != DocumentStatus.Disposed);
    }

    /// <summary>
    /// When workspace classification is UPGRADED, bound documents with LESS restrictive
    /// classification are upgraded to match the workspace level.
    /// Documents are NEVER downgraded via workspace cascade.
    /// </summary>
    public IEnumerable<Document> GetDocumentsForClassificationUpgrade(
        ClassificationLevel newWorkspaceLevel,
        IEnumerable<WorkspaceDocument> primaryBindings,
        IEnumerable<Document> documents)
    {
        var primaryDocIds = primaryBindings
            .Where(b => b.BindingType == "Primary" && b.IsActive)
            .Select(b => b.DocumentId)
            .ToHashSet();

        return documents.Where(d =>
            primaryDocIds.Contains(d.DocumentId)
            && newWorkspaceLevel.IsMoreRestrictiveThan(d.Classification));
    }

    /// <summary>
    /// When a document is first bound to a workspace, inherit governance settings
    /// that are not already set on the document.
    /// </summary>
    public GovernanceInheritance ComputeGovernanceInheritance(
        Workspace workspace, Document document)
    {
        return new GovernanceInheritance(
            InheritClassification: workspace.Classification.IsMoreRestrictiveThan(document.Classification),
            InheritRetentionPolicy: !document.RetentionExpiresAt.HasValue && workspace.RetentionPolicyId.HasValue,
            InheritWorkflow: workspace.DefaultWorkflowId.HasValue,
            NewClassification: workspace.Classification,
            NewRetentionPolicyId: workspace.RetentionPolicyId,
            DefaultWorkflowId: workspace.DefaultWorkflowId);
    }
}

public sealed record GovernanceInheritance(
    bool InheritClassification,
    bool InheritRetentionPolicy,
    bool InheritWorkflow,
    ClassificationLevel NewClassification,
    int? NewRetentionPolicyId,
    int? DefaultWorkflowId);
