using Darah.ECM.Application.Common.Correlation;
using Darah.ECM.Application.Common.Guards;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Application.Common.Errors;

// ─── STANDARD ERROR CODES ────────────────────────────────────────────────────
/// <summary>
/// Standardized error codes for all modules.
/// Prevents information leakage by mapping internal errors to user-safe messages.
/// Error codes are stable across deployments — clients can safely match on them.
/// </summary>
public static class ErrorCodes
{
    // General
    public const string NotFound         = "ECM_001";
    public const string ValidationFailed = "ECM_002";
    public const string Unauthorized     = "ECM_003";
    public const string Forbidden        = "ECM_004";
    public const string Conflict         = "ECM_005";
    public const string ConcurrencyError = "ECM_006";
    public const string InternalError    = "ECM_099";

    // Document
    public const string DocumentNotFound    = "DOC_001";
    public const string DocumentCheckedOut  = "DOC_002";
    public const string DocumentLegalHold   = "DOC_003";
    public const string DocumentInvalidFile = "DOC_004";
    public const string DocumentVersionConflict = "DOC_005";

    // Workflow
    public const string WorkflowNotFound       = "WF_001";
    public const string WorkflowAlreadyActive  = "WF_002";
    public const string WorkflowTaskNotAssigned = "WF_003";
    public const string WorkflowInvalidAction  = "WF_004";

    // Records
    public const string RecordAlreadyDeclared  = "REC_001";
    public const string RetentionPolicyInvalid = "REC_002";
    public const string LegalHoldActive        = "REC_003";
    public const string DisposalBlockedHold    = "REC_004";
}

// ─── ERROR RESPONSE BUILDER ───────────────────────────────────────────────────
/// <summary>
/// Builds standardized API error responses — no information leakage.
/// Internal error details logged to Serilog but NOT returned to clients.
/// </summary>
public static class ErrorResponseBuilder
{
    public static ApiResponse<T> Build<T>(
        string errorCode,
        string userMessage,
        Exception? ex = null,
        ILogger? logger = null,
        string? correlationId = null)
    {
        if (ex is not null && logger is not null)
            logger.LogError(ex,
                "Error {ErrorCode}: {Message} | CorrelationId={CorrelationId}",
                errorCode, ex.Message, correlationId ?? "unknown");

        return ApiResponse<T>.Fail(userMessage, new[] { errorCode });
    }

    public static ApiResponse<T> NotFound<T>(string entityNameAr, string? id = null)
        => ApiResponse<T>.Fail(
            $"{entityNameAr} غير {(id is not null ? $"موجود (معرف: {id})" : "موجود")}",
            new[] { ErrorCodes.NotFound });

    public static ApiResponse<T> LegalHoldBlocked<T>()
        => ApiResponse<T>.Fail(
            "العملية مرفوضة: الوثيقة خاضعة لتجميد قانوني",
            new[] { ErrorCodes.LegalHoldActive });

    public static ApiResponse<T> ConcurrencyConflict<T>(string entityAr)
        => ApiResponse<T>.Fail(
            $"تعارض في التزامن: تم تعديل {entityAr} بواسطة مستخدم آخر. يرجى التحديث والمحاولة مجدداً",
            new[] { ErrorCodes.ConcurrencyError });

    public static ApiResponse<T> InternalError<T>(Exception ex, ILogger logger, string correlationId)
    {
        logger.LogError(ex, "Unhandled error | CorrelationId={CorrelationId}", correlationId);
        return ApiResponse<T>.Fail(
            "حدث خطأ داخلي في النظام. يرجى المحاولة لاحقاً أو التواصل مع الدعم الفني",
            new[] { ErrorCodes.InternalError });
    }
}

// ─── CROSS-MODULE ATOMIC: Document → Workflow ─────────────────────────────────
/// <summary>
/// Atomically submits a document to workflow AND updates its status to Pending.
/// Both changes in a single DB transaction. Rollback if either fails.
///
/// Multi-module transaction pattern:
///   BeginTransaction()
///   ├─ Document.TransitionStatus(Pending)  [Document module]
///   ├─ WorkflowInstance.Start()            [Workflow module]
///   ├─ WorkflowTask.Create(firstStep)      [Workflow module]
///   └─ CommitTransaction()
///   DispatchDomainEventsAsync()            [post-commit, never on rollback]
/// </summary>
public sealed class SubmitDocumentToWorkflowUseCase
{
    private readonly IUnitOfWork              _uow;
    private readonly ICurrentUser             _user;
    private readonly IWorkflowEngine          _engine;
    private readonly IStructuredAuditService  _audit;
    private readonly IAuthorizationGuard      _authGuard;
    private readonly DocumentLifecycleService _lifecycle;
    private readonly ILogger<SubmitDocumentToWorkflowUseCase> _logger;

    public SubmitDocumentToWorkflowUseCase(
        IUnitOfWork uow, ICurrentUser user, IWorkflowEngine engine,
        IStructuredAuditService audit, IAuthorizationGuard authGuard,
        DocumentLifecycleService lifecycle,
        ILogger<SubmitDocumentToWorkflowUseCase> logger)
    {
        _uow = uow; _user = user; _engine = engine;
        _audit = audit; _authGuard = authGuard; _lifecycle = lifecycle; _logger = logger;
    }

    public async Task<ApiResponse<int>> ExecuteAsync(
        Guid documentId, int? definitionId, int priority,
        string? comment, CancellationToken ct)
    {
        // Authorization
        var document = await _uow.Documents.GetByGuidAsync(documentId, ct);
        if (document is null)
            return ErrorResponseBuilder.NotFound<int>("الوثيقة", documentId.ToString());

        var authResult = await _authGuard.AuthorizeDocumentAsync(
            documentId, "workflow.submit", document, ct);
        if (!authResult.IsGranted)
            return authResult.ToFailResponse<int>();

        // Domain lifecycle validation (single source of truth)
        var lifecycleResult = _lifecycle.TransitionToWorkflowPending(document, _user.UserId);
        if (!lifecycleResult.IsSuccess)
            return ApiResponse<int>.Fail(lifecycleResult.Error!);

        // Check no active workflow
        var existing = await _uow.Workflows.GetActiveForDocumentAsync(documentId, ct);
        if (existing is not null)
            return ApiResponse<int>.Fail("يوجد مسار عمل نشط لهذه الوثيقة بالفعل",
                new[] { ErrorCodes.WorkflowAlreadyActive });

        // Detect definition
        definitionId ??= await _engine.DetectWorkflowDefinitionAsync(document.DocumentTypeId, ct);
        if (!definitionId.HasValue)
            return ApiResponse<int>.Fail("لا يوجد مسار عمل محدد لهذا النوع من الوثائق");

        // ATOMIC: Start workflow + update document status in single transaction
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var instanceId = await _engine.StartAsync(
                documentId, definitionId.Value, _user.UserId, priority, ct);

            await _uow.CommitAsync(ct); // persist workflow
            await _uow.CommitAsync(ct); // persist document status
            await _uow.CommitTransactionAsync(ct);

            _logger.LogInformation(
                "Document {DocId} submitted to workflow {InstanceId}",
                documentId, instanceId);

            await _uow.DispatchDomainEventsAsync(ct);

            await _audit.LogSuccessAsync("WorkflowSubmitted", AuditEntry.Modules.Workflow,
                "Document", documentId.ToString(),
                newValues: new { instanceId, priority }, ct: ct);

            return ApiResponse<int>.Ok(instanceId, "تم إرسال الوثيقة لمسار الاعتماد بنجاح");
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            _logger.LogError(ex, "Submit to workflow failed for document {DocId}", documentId);
            return ErrorResponseBuilder.InternalError<int>(ex, _logger,
                _audit is StructuredAuditService ? "" : "");
        }
    }
}

// ─── CROSS-MODULE ATOMIC: Document → Record Declaration ──────────────────────
/// <summary>
/// Atomically declares a record AND applies retention policy in a single transaction.
///
/// Multi-module transaction:
///   BeginTransaction()
///   ├─ Validate domain rules (DocumentLifecycleService)
///   ├─ document.AssignRecordClass()
///   ├─ document.SetRetentionExpiry()
///   ├─ document.TransitionStatus(Active) if Draft
///   └─ CommitTransaction()
///   DispatchDomainEventsAsync()
/// </summary>
public sealed class DeclareDocumentRecordUseCase
{
    private readonly IUnitOfWork              _uow;
    private readonly ICurrentUser             _user;
    private readonly IRecordsRepository       _recordsRepo;
    private readonly IStructuredAuditService  _audit;
    private readonly IAuthorizationGuard      _authGuard;
    private readonly DocumentLifecycleService _lifecycle;

    public DeclareDocumentRecordUseCase(IUnitOfWork uow, ICurrentUser user,
        IRecordsRepository recordsRepo, IStructuredAuditService audit,
        IAuthorizationGuard authGuard, DocumentLifecycleService lifecycle)
    {
        _uow = uow; _user = user; _recordsRepo = recordsRepo;
        _audit = audit; _authGuard = authGuard; _lifecycle = lifecycle;
    }

    public async Task<ApiResponse<RecordDeclaredDto>> ExecuteAsync(
        Guid documentId, int recordClassId, int retentionPolicyId,
        string? note, CancellationToken ct)
    {
        var document = await _uow.Documents.GetByGuidAsync(documentId, ct);
        if (document is null)
            return ErrorResponseBuilder.NotFound<RecordDeclaredDto>("الوثيقة", documentId.ToString());

        var authResult = await _authGuard.AuthorizeAdminActionAsync("admin.retention", ct);
        if (!authResult.IsGranted) return authResult.ToFailResponse<RecordDeclaredDto>();

        var lifecycleResult = _lifecycle.ValidateRecordDeclaration(document);
        if (!lifecycleResult.IsSuccess)
            return ApiResponse<RecordDeclaredDto>.Fail(lifecycleResult.Error!,
                new[] { ErrorCodes.RecordAlreadyDeclared });

        var policy = await _recordsRepo.GetRetentionPolicyAsync(retentionPolicyId, ct);
        if (policy is null)
            return ApiResponse<RecordDeclaredDto>.Fail("سياسة الاحتفاظ غير موجودة",
                new[] { ErrorCodes.RetentionPolicyInvalid });

        await _uow.BeginTransactionAsync(ct);
        try
        {
            document.AssignRecordClass(recordClassId, _user.UserId);
            var trigger = document.DocumentDate ?? DateOnly.FromDateTime(document.CreatedAt);
            document.SetRetentionExpiry(policy.ComputeExpiry(trigger), _user.UserId);

            if (document.Status == Domain.ValueObjects.DocumentStatus.Draft)
            {
                var result = _lifecycle.TransitionToActive(document, _user.UserId);
                if (!result.IsSuccess)
                    return ApiResponse<RecordDeclaredDto>.Fail(result.Error!);
            }

            await _uow.CommitAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            await _uow.DispatchDomainEventsAsync(ct);

            await _audit.LogSuccessAsync("RecordDeclared", AuditEntry.Modules.Records,
                "Document", documentId.ToString(),
                newValues: new { recordClassId, retentionPolicyId, policy.NameAr }, ct: ct);

            return ApiResponse<RecordDeclaredDto>.Ok(new RecordDeclaredDto(
                documentId, document.DocumentNumber, recordClassId,
                policy.NameAr, document.RetentionExpiresAt!.Value, policy.DisposalAction),
                "تم تصنيف الوثيقة كسجل رسمي بنجاح");
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return ErrorResponseBuilder.InternalError<RecordDeclaredDto>(ex,
                Microsoft.Extensions.Logging.LoggerFactory.Create(_ => { })
                    .CreateLogger<DeclareDocumentRecordUseCase>(), "");
        }
    }
}

public sealed record RecordDeclaredDto(
    Guid DocumentId, string DocumentNumber, int RecordClassId,
    string RetentionPolicyName, DateOnly RetentionExpiryDate, string DisposalAction);
