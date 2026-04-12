using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Notifications;
using MediatR;

namespace Darah.ECM.Application.Workflow.Queries;

/// <summary>User's workflow inbox — tasks awaiting action.</summary>
public sealed record GetWorkflowInboxQuery(
    string? Status      = "Pending",
    bool    OverdueOnly = false,
    int     Page        = 1,
    int     PageSize    = 20)
    : IRequest<ApiResponse<PagedResult<InboxItemDto>>>;

/// <summary>Detailed view of a single workflow task with full action history.</summary>
public sealed record GetWorkflowTaskDetailQuery(int TaskId)
    : IRequest<ApiResponse<WorkflowTaskDto>>;

/// <summary>Full action history for a workflow instance.</summary>
public sealed record GetWorkflowHistoryQuery(int InstanceId)
    : IRequest<ApiResponse<List<WorkflowActionDto>>>;

/// <summary>Workflow dashboard summary for the current user.</summary>
public sealed record GetWorkflowSummaryQuery()
    : IRequest<ApiResponse<WorkflowSummaryDto>>;

/// <summary>All workflow definitions (admin view).</summary>
public sealed record GetWorkflowDefinitionsQuery(bool? IsActive = null)
    : IRequest<ApiResponse<List<WorkflowDefinitionDto>>>;

/// <summary>Workflow instance history for a specific document.</summary>
public sealed record GetDocumentWorkflowHistoryQuery(Guid DocumentId)
    : IRequest<ApiResponse<List<WorkflowInstanceDto>>>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public sealed record WorkflowStepDto(
    int      StepId,
    string   StepCode,
    string   NameAr,
    int      StepOrder,
    string   StepType,
    string   AssigneeType,
    int?     SLAHours,
    int?     EscalationHours,
    bool     IsFirstStep,
    bool     IsFinalStep,
    bool     AllowReject,
    bool     AllowReturn,
    bool     AllowDelegate);
