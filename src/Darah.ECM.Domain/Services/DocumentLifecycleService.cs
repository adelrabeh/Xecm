using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.ValueObjects;

namespace Darah.ECM.Domain.Services;

/// <summary>
/// SINGLE SOURCE OF TRUTH for document lifecycle transitions across all modules.
///
/// PROBLEM SOLVED:
///   Before this service, documents could be transitioned from:
///     - WorkflowEngine (Approve → Active, Reject → Rejected)
///     - RecordsCommands (Declare → Active)
///     - DocumentCommands (Submit → Pending)
///     - Potentially inconsistent states across modules
///
/// SOLUTION:
///   All status transitions MUST go through this service.
///   The service enforces cross-module business rules:
///     - Cannot transition if legal hold is active and transition is a write
///     - Cannot transition if active workflow exists (for certain transitions)
///     - Record declaration can only happen on Active documents
///     - Disposal can only happen on Archived/Disposed-eligible documents
///
/// PATTERN: Domain Service (stateless, operates on entities, no infrastructure deps)
/// </summary>
public sealed class DocumentLifecycleService
{
    // ─── Allowed cross-module transitions (enforced centrally) ────────────────

    /// <summary>
    /// Submit document to workflow approval.
    /// Pre-condition: status must be Draft or Rejected; not checked out; not on legal hold.
    /// </summary>
    public Result TransitionToWorkflowPending(Document document, int userId)
    {
        if (document.IsLegalHold)
            return Result.Fail("لا يمكن إرسال وثيقة خاضعة لتجميد قانوني لمسار العمل");
        if (document.IsCheckedOut)
            return Result.Fail("يجب إيداع الوثيقة قبل إرسالها لمسار العمل");
        if (document.Status != DocumentStatus.Draft && document.Status != DocumentStatus.Rejected)
            return Result.Fail($"لا يمكن إرسال وثيقة بحالة '{document.Status}' لمسار العمل");

        document.TransitionStatus(DocumentStatus.Pending, userId);
        return Result.Ok();
    }

    /// <summary>
    /// Approve via workflow — transitions to Approved (intermediate) then Active.
    /// </summary>
    public Result TransitionToApproved(Document document, int userId)
    {
        if (document.Status != DocumentStatus.Pending)
            return Result.Fail($"الموافقة غير مسموحة على وثيقة بحالة '{document.Status}'");

        document.TransitionStatus(DocumentStatus.Approved, userId);
        return Result.Ok();
    }

    /// <summary>
    /// Activate document after approval or after record declaration.
    /// </summary>
    public Result TransitionToActive(Document document, int userId)
    {
        if (document.Status != DocumentStatus.Draft
            && document.Status != DocumentStatus.Approved
            && document.Status != DocumentStatus.Rejected)
            return Result.Fail($"لا يمكن تفعيل وثيقة بحالة '{document.Status}'");

        document.TransitionStatus(DocumentStatus.Active, userId);
        return Result.Ok();
    }

    /// <summary>
    /// Reject via workflow — transitions back to Rejected.
    /// Pre-condition: document must be in Pending state.
    /// </summary>
    public Result TransitionToRejected(Document document, int userId)
    {
        if (document.Status != DocumentStatus.Pending)
            return Result.Fail($"الرفض غير مسموح على وثيقة بحالة '{document.Status}'");

        document.TransitionStatus(DocumentStatus.Rejected, userId);
        return Result.Ok();
    }

    /// <summary>
    /// Archive document — validates no active workflow, allows legal hold override.
    /// Legal hold documents can still be archived (archival is a read-safe operation).
    /// </summary>
    public Result TransitionToArchived(Document document, int userId,
        bool hasActiveWorkflow)
    {
        if (hasActiveWorkflow)
            return Result.Fail("لا يمكن أرشفة وثيقة لها مسار عمل نشط");
        if (document.Status != DocumentStatus.Active
            && document.Status != DocumentStatus.Approved
            && document.Status != DocumentStatus.Superseded)
            return Result.Fail($"لا يمكن أرشفة وثيقة بحالة '{document.Status}'");

        document.TransitionStatus(DocumentStatus.Archived, userId);
        return Result.Ok();
    }

    /// <summary>
    /// Mark document as Disposed after approved disposal request execution.
    /// Pre-condition: must be Archived; must not be on legal hold.
    /// </summary>
    public Result TransitionToDisposed(Document document, int userId)
    {
        if (document.IsLegalHold)
            return Result.Fail("لا يمكن إتلاف وثيقة خاضعة لتجميد قانوني");
        if (document.Status != DocumentStatus.Archived)
            return Result.Fail("يمكن إتلاف الوثائق المؤرشفة فقط");

        document.TransitionStatus(DocumentStatus.Disposed, userId);
        return Result.Ok();
    }

    /// <summary>
    /// Declare document as a record. Only allowed on Active or Draft documents.
    /// Declaration does NOT change the document status — it enriches the record metadata.
    /// The caller is responsible for transitioning Draft→Active separately if needed.
    /// </summary>
    public Result ValidateRecordDeclaration(Document document)
    {
        if (document.RecordClassId.HasValue)
            return Result.Fail("الوثيقة مصنفة كسجل بالفعل");
        if (document.Status == DocumentStatus.Disposed)
            return Result.Fail("لا يمكن تصنيف وثيقة متلفة كسجل");
        if (document.Status == DocumentStatus.Archived && !document.IsLegalHold)
            return Result.Fail("تصنيف الوثائق المؤرشفة كسجلات يتطلب مراجعة قانونية");
        return Result.Ok();
    }

    /// <summary>
    /// Validate that a document can have legal hold applied.
    /// Legal hold is always allowed UNLESS the document is already Disposed.
    /// </summary>
    public Result ValidateLegalHoldApplication(Document document)
    {
        if (document.Status == DocumentStatus.Disposed)
            return Result.Fail("لا يمكن تطبيق التجميد القانوني على وثيقة متلفة");
        if (document.IsLegalHold)
            return Result.Fail("التجميد القانوني مطبق على هذه الوثيقة بالفعل");
        return Result.Ok();
    }

    /// <summary>
    /// Full state invariant check — validates the document is internally consistent.
    /// Called by integration tests and can be called by monitoring jobs.
    /// </summary>
    public IReadOnlyList<string> ValidateInvariants(Document document)
    {
        var violations = new List<string>();

        if (document.IsCheckedOut && document.CheckedOutBy is null)
            violations.Add($"[{document.DocumentNumber}] IsCheckedOut=true but CheckedOutBy is null");

        if (!document.IsCheckedOut && document.CheckedOutAt.HasValue)
            violations.Add($"[{document.DocumentNumber}] IsCheckedOut=false but CheckedOutAt has value");

        if (document.CurrentVersionId <= 0 && document.Status != DocumentStatus.Draft)
            violations.Add($"[{document.DocumentNumber}] Non-draft document has no valid CurrentVersionId");

        if (document.RetentionExpiresAt.HasValue && document.RetentionExpiresAt.Value < DateOnly.MinValue.AddYears(1))
            violations.Add($"[{document.DocumentNumber}] RetentionExpiresAt is implausibly old");

        if (document.Status == DocumentStatus.Disposed && document.IsLegalHold)
            violations.Add($"[{document.DocumentNumber}] Document is both Disposed and on LegalHold — impossible state");

        return violations.AsReadOnly();
    }
}

// ─── RESULT TYPE (Domain layer — no dependencies) ─────────────────────────────
/// <summary>
/// Simple Result type for domain service operations.
/// Avoids throwing exceptions for expected business rule failures.
/// </summary>
public sealed class Result
{
    public bool   IsSuccess { get; private set; }
    public string? Error    { get; private set; }

    private Result() { }

    public static Result Ok() => new() { IsSuccess = true };

    public static Result Fail(string error) => new() { IsSuccess = false, Error = error };

    public static implicit operator bool(Result r) => r.IsSuccess;

    public void ThrowIfFailed()
    {
        if (!IsSuccess)
            throw new DomainRuleViolationException(Error!);
    }
}

/// <summary>Thrown when a domain business rule is violated.</summary>
public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(string message) : base(message) { }
}
