using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Application.Workflow.Commands;

// ─── SUBMIT TO WORKFLOW ───────────────────────────────────────────────────────
public sealed record SubmitToWorkflowCommand(
    Guid    DocumentId,
    int?    WorkflowDefinitionId,  // null = auto-detect from document type
    int     Priority = 2,
    string? Comment  = null)
    : IRequest<ApiResponse<WorkflowInstanceDto>>;

public sealed class SubmitToWorkflowCommandHandler
    : IRequestHandler<SubmitToWorkflowCommand, ApiResponse<WorkflowInstanceDto>>
{
    private readonly IUnitOfWork      _uow;
    private readonly IWorkflowEngine  _workflowEngine;
    private readonly ICurrentUser     _user;
    private readonly IAuditService    _audit;
    private readonly INotificationService _notifier;
    private readonly ILogger<SubmitToWorkflowCommandHandler> _logger;

    public SubmitToWorkflowCommandHandler(IUnitOfWork uow, IWorkflowEngine workflowEngine,
        ICurrentUser user, IAuditService audit, INotificationService notifier,
        ILogger<SubmitToWorkflowCommandHandler> logger)
    {
        _uow = uow; _workflowEngine = workflowEngine; _user = user;
        _audit = audit; _notifier = notifier; _logger = logger;
    }

    public async Task<ApiResponse<WorkflowInstanceDto>> Handle(
        SubmitToWorkflowCommand cmd, CancellationToken ct)
    {
        var document = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (document is null) return ApiResponse<WorkflowInstanceDto>.Fail("الوثيقة غير موجودة");
        if (!document.CanSubmitToWorkflow())
            return ApiResponse<WorkflowInstanceDto>.Fail(
                document.IsCheckedOut  ? "الوثيقة محجوزة. يجب إيداعها أولاً" :
                document.IsLegalHold   ? "الوثيقة خاضعة لتجميد قانوني" :
                "حالة الوثيقة لا تسمح بإرسالها لسير العمل");

        // Detect workflow definition
        int? defId = cmd.WorkflowDefinitionId;
        if (!defId.HasValue)
            defId = await _workflowEngine.DetectWorkflowDefinitionAsync(
                document.DocumentTypeId, ct);

        if (!defId.HasValue)
            return ApiResponse<WorkflowInstanceDto>.Fail(
                "لا يوجد مسار عمل محدد لهذا النوع من الوثائق");

        // Check no active workflow exists
        var existingInstance = await _uow.Workflows.GetActiveForDocumentAsync(cmd.DocumentId, ct);
        if (existingInstance is not null)
            return ApiResponse<WorkflowInstanceDto>.Fail("يوجد مسار عمل نشط لهذه الوثيقة بالفعل");

        // Start workflow
        var instanceId = await _workflowEngine.StartAsync(
            cmd.DocumentId, defId.Value, _user.UserId, cmd.Priority, ct);

        // Update document status to Pending
        document.TransitionStatus(Domain.ValueObjects.DocumentStatus.Pending, _user.UserId);
        await _uow.CommitAsync(ct);
        await _uow.DispatchDomainEventsAsync(ct);

        await _audit.LogAsync("WorkflowSubmitted", "WorkflowInstance", instanceId.ToString(),
            additionalInfo: $"DocumentId={cmd.DocumentId} Priority={cmd.Priority}", ct: ct);

        _logger.LogInformation("Workflow started: InstanceId={Id} DocumentId={DocId}",
            instanceId, cmd.DocumentId);

        return ApiResponse<WorkflowInstanceDto>.Ok(
            new WorkflowInstanceDto(instanceId, cmd.DocumentId, "InProgress",
                defId.Value, null, cmd.Priority, DateTime.UtcNow, null, new()),
            "تم إرسال الوثيقة لمسار الاعتماد بنجاح");
    }
}

// ─── WORKFLOW ACTION COMMAND ──────────────────────────────────────────────────
public sealed record WorkflowActionCommand(
    int     TaskId,
    string  ActionType,        // Approve|Reject|Return|Delegate|Comment
    string? Comment,
    int?    DelegateToUserId)
    : IRequest<ApiResponse<bool>>;

public sealed class WorkflowActionCommandValidator : AbstractValidator<WorkflowActionCommand>
{
    public WorkflowActionCommandValidator()
    {
        RuleFor(x => x.TaskId).GreaterThan(0).WithMessage("معرف المهمة غير صحيح");
        RuleFor(x => x.ActionType).NotEmpty()
            .Must(a => new[] { "Approve", "Reject", "Return", "Delegate", "Comment" }.Contains(a))
            .WithMessage("نوع الإجراء غير صحيح");
        When(x => x.ActionType == "Reject" || x.ActionType == "Return", () =>
            RuleFor(x => x.Comment).NotEmpty().WithMessage("يجب إدخال سبب الإجراء"));
        When(x => x.ActionType == "Delegate", () =>
            RuleFor(x => x.DelegateToUserId).NotNull().GreaterThan(0)
                .WithMessage("يجب تحديد المستخدم للتفويض"));
    }
}

public sealed class WorkflowActionCommandHandler
    : IRequestHandler<WorkflowActionCommand, ApiResponse<bool>>
{
    private readonly IWorkflowEngine  _engine;
    private readonly ICurrentUser     _user;
    private readonly IAuditService    _audit;
    private readonly INotificationService _notifier;

    public WorkflowActionCommandHandler(IWorkflowEngine engine, ICurrentUser user,
        IAuditService audit, INotificationService notifier)
        { _engine = engine; _user = user; _audit = audit; _notifier = notifier; }

    public async Task<ApiResponse<bool>> Handle(
        WorkflowActionCommand cmd, CancellationToken ct)
    {
        var success = await _engine.ProcessActionAsync(
            cmd.TaskId, cmd.ActionType, _user.UserId,
            cmd.Comment, cmd.DelegateToUserId, ct);

        if (!success)
            return ApiResponse<bool>.Fail(
                "فشل في تنفيذ الإجراء. تأكد من صلاحياتك وأن المهمة معينة لك");

        await _audit.LogAsync($"Workflow{cmd.ActionType}", "WorkflowTask",
            cmd.TaskId.ToString(), additionalInfo: $"Comment={cmd.Comment}", ct: ct);

        return ApiResponse<bool>.Ok(true, $"تم تنفيذ الإجراء '{cmd.ActionType}' بنجاح");
    }
}

// ─── CREATE DELEGATION ────────────────────────────────────────────────────────
public sealed record CreateDelegationCommand(
    int     ToUserId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason)
    : IRequest<ApiResponse<bool>>;

public sealed class CreateDelegationCommandValidator : AbstractValidator<CreateDelegationCommand>
{
    public CreateDelegationCommandValidator()
    {
        RuleFor(x => x.ToUserId).GreaterThan(0).WithMessage("يجب تحديد المستخدم");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");
        RuleFor(x => x.StartDate).GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("تاريخ البدء لا يمكن أن يكون في الماضي");
    }
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────
namespace Darah.ECM.Application.Workflow.DTOs;

public sealed record WorkflowInstanceDto(
    int      InstanceId,
    Guid     DocumentId,
    string   Status,
    int      DefinitionId,
    int?     CurrentStepId,
    int      Priority,
    DateTime StartedAt,
    DateTime? CompletedAt,
    List<WorkflowTaskDto> Tasks);

public sealed record WorkflowTaskDto(
    int      TaskId,
    int      InstanceId,
    string   StepNameAr,
    string?  StepNameEn,
    string?  AssignedToNameAr,
    string   Status,
    DateTime AssignedAt,
    DateTime? DueAt,
    bool     IsOverdue,
    bool     IsDelegated,
    List<WorkflowActionDto> Actions);

public sealed record WorkflowActionDto(
    int      ActionId,
    string   ActionType,
    string?  Comment,
    DateTime ActionAt,
    string?  ActionByNameAr);

public sealed record InboxItemDto(
    int      TaskId,
    int      InstanceId,
    Guid     DocumentId,
    string   DocumentTitleAr,
    string   WorkflowNameAr,
    string   StepNameAr,
    string   Status,
    DateTime AssignedAt,
    DateTime? DueAt,
    bool     IsOverdue,
    int      Priority,
    string?  DocumentTypeNameAr,
    string?  DocumentNumber);

public sealed record WorkflowSummaryDto(
    int TotalPending,
    int TotalOverdue,
    int TotalDueToday,
    int TotalDelegated,
    double AvgCompletionHours,
    double SlaCompliancePercent);
