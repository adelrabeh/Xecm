using Darah.ECM.API.Filters;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Workflow.Commands;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Notifications;
using Darah.ECM.Application.Workflow.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.API.Controllers.v1;

[ApiController]
[Route("api/v1/workflow")]
[Authorize]
[Produces("application/json")]
public sealed class WorkflowController : ControllerBase
{
    private readonly IMediator _mediator;
    public WorkflowController(IMediator mediator) => _mediator = mediator;

    // ── Definitions ───────────────────────────────────────────────────────────
    [HttpGet("definitions")]
    [RequirePermission("workflow.manage")]
    public async Task<ActionResult<ApiResponse<List<WorkflowDefinitionDto>>>> GetDefinitions(
        [FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWorkflowDefinitionsQuery(isActive), ct));

    // ── Submit ────────────────────────────────────────────────────────────────
    [HttpPost("submit/{documentId:guid}")]
    [RequirePermission("workflow.submit")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceDto>>> Submit(
        Guid documentId, [FromBody] SubmitWorkflowRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new SubmitToWorkflowCommand(
            documentId, req.WorkflowDefinitionId, req.Priority, req.Comment), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Inbox ─────────────────────────────────────────────────────────────────
    [HttpGet("inbox")]
    public async Task<ActionResult<ApiResponse<PagedResult<InboxItemDto>>>> Inbox(
        [FromQuery] string? status = "Pending", [FromQuery] bool overdueOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetWorkflowInboxQuery(status, overdueOnly, page, pageSize), ct));

    [HttpGet("inbox/summary")]
    public async Task<ActionResult<ApiResponse<WorkflowSummaryDto>>> Summary(CancellationToken ct)
        => Ok(await _mediator.Send(new GetWorkflowSummaryQuery(), ct));

    // ── Task Detail ───────────────────────────────────────────────────────────
    [HttpGet("tasks/{taskId:int}")]
    public async Task<ActionResult<ApiResponse<WorkflowTaskDto>>> GetTask(
        int taskId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWorkflowTaskDetailQuery(taskId), ct);
        return result.Data is null ? NotFound(ApiResponse<WorkflowTaskDto>.Fail("المهمة غير موجودة")) : Ok(result);
    }

    // ── Actions ───────────────────────────────────────────────────────────────
    [HttpPost("tasks/{taskId:int}/approve")]
    [RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Approve(
        int taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
        => Handle(await _mediator.Send(
            new WorkflowActionCommand(taskId, "Approve", req.Comment, null), ct));

    [HttpPost("tasks/{taskId:int}/reject")]
    [RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Reject(
        int taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Comment))
            return BadRequest(ApiResponse<bool>.Fail("يجب إدخال سبب الرفض"));
        return Handle(await _mediator.Send(
            new WorkflowActionCommand(taskId, "Reject", req.Comment, null), ct));
    }

    [HttpPost("tasks/{taskId:int}/return")]
    [RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Return(
        int taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Comment))
            return BadRequest(ApiResponse<bool>.Fail("يجب إدخال سبب الإرجاع"));
        return Handle(await _mediator.Send(
            new WorkflowActionCommand(taskId, "Return", req.Comment, null), ct));
    }

    [HttpPost("tasks/{taskId:int}/delegate")]
    [RequirePermission("workflow.delegate")]
    public async Task<ActionResult<ApiResponse<bool>>> Delegate(
        int taskId, [FromBody] DelegateTaskRequest req, CancellationToken ct)
    {
        if (req.ToUserId <= 0)
            return BadRequest(ApiResponse<bool>.Fail("يجب تحديد المستخدم المفوض إليه"));
        return Handle(await _mediator.Send(
            new WorkflowActionCommand(taskId, "Delegate", req.Comment, req.ToUserId), ct));
    }

    [HttpPost("tasks/{taskId:int}/comment")]
    public async Task<ActionResult<ApiResponse<bool>>> Comment(
        int taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Comment))
            return BadRequest(ApiResponse<bool>.Fail("يجب إدخال تعليق"));
        return Handle(await _mediator.Send(
            new WorkflowActionCommand(taskId, "Comment", req.Comment, null), ct));
    }

    // ── History ───────────────────────────────────────────────────────────────
    [HttpGet("instances/{instanceId:int}/history")]
    public async Task<ActionResult<ApiResponse<List<WorkflowActionDto>>>> History(
        int instanceId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWorkflowHistoryQuery(instanceId), ct));

    [HttpGet("documents/{documentId:guid}/history")]
    public async Task<ActionResult<ApiResponse<List<WorkflowInstanceDto>>>> DocumentHistory(
        Guid documentId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDocumentWorkflowHistoryQuery(documentId), ct));

    // ── Delegation Management ─────────────────────────────────────────────────
    [HttpPost("delegations")]
    public async Task<ActionResult<ApiResponse<bool>>> CreateDelegation(
        [FromBody] CreateDelegationCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private ActionResult<ApiResponse<bool>> Handle(ApiResponse<bool> r)
        => r.Success ? Ok(r) : BadRequest(r);
}

// ─── REQUEST MODELS ───────────────────────────────────────────────────────────
public sealed record SubmitWorkflowRequest(
    int? WorkflowDefinitionId, int Priority = 2, string? Comment = null);

public sealed record WorkflowActionRequest(string? Comment);

public sealed record DelegateTaskRequest(int ToUserId, string? Comment);
