using Darah.ECM.API.Filters;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Workflow.Commands;
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
    public async Task<ActionResult<ApiResponse<List<WorkflowDefinitionDto>>>> GetDefinitions(
        [FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWorkflowDefinitionsQuery(isActive), ct));

    // ── Submit ────────────────────────────────────────────────────────────────
    [HttpPost("submit/{documentId:guid}")]
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
    public async Task<ActionResult<ApiResponse<bool>>> Approve(
        int taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
        => Handle(await _mediator.Send(
            new WorkflowActionCommand(taskId, "Approve", req.Comment, null), ct));

    [HttpPost("tasks/{taskId:int}/reject")]
    public async Task<ActionResult<ApiResponse<bool>>> Reject(
        int taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Comment))
            return BadRequest(ApiResponse<bool>.Fail("يجب إدخال سبب الرفض"));
        return Handle(await _mediator.Send(
            new WorkflowActionCommand(taskId, "Reject", req.Comment, null), ct));
    }

    [HttpPost("tasks/{taskId:int}/return")]
    public async Task<ActionResult<ApiResponse<bool>>> Return(
        int taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Comment))
            return BadRequest(ApiResponse<bool>.Fail("يجب إدخال سبب الإرجاع"));
        return Handle(await _mediator.Send(
            new WorkflowActionCommand(taskId, "Return", req.Comment, null), ct));
    }

    [HttpPost("tasks/{taskId:int}/delegate")]
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

// ─── WORKFLOW TEMPLATES ────────────────────────────────────────────────────────
/// <summary>Get predefined workflow templates for quick start.</summary>
[HttpGet("templates")]
public IActionResult GetTemplates()
{
    var templates = new[]
    {
        new { id=1, code="NEW_TASK",       nameAr="تكليف مهمة جديدة",         nameEn="New Task Assignment",      icon="📋", steps=1, description="تكليف مباشر لمستخدم أو مجموعة" },
        new { id=2, code="GROUP_REVIEW",   nameAr="مراجعة واعتماد جماعي",      nameEn="Group Review & Approval",  icon="👥", steps=2, description="مراجعة من مجموعة ثم اعتماد" },
        new { id=3, code="MULTI_REVIEW",   nameAr="اعتماد متعدد المراجعين",    nameEn="Multi-Reviewer Approval",  icon="✅", steps=3, description="مراجعة تسلسلية من عدة مستخدمين" },
        new { id=4, code="POOLED_REVIEW",  nameAr="مراجعة من مجموعة مشتركة",  nameEn="Pooled Group Review",      icon="🔄", steps=2, description="أي عضو من المجموعة يمكنه المطالبة بالمهمة" },
        new { id=5, code="SINGLE_APPROVE", nameAr="اعتماد مستخدم واحد",       nameEn="Single Reviewer Approval", icon="👤", steps=1, description="اعتماد من شخص محدد" },
    };
    return Ok(ApiResponse<object>.Ok(templates));
}

/// <summary>Get workflows initiated by current user.</summary>
[HttpGet("my-workflows")]
public async Task<ActionResult<ApiResponse<object>>> GetMyWorkflows(
    [FromQuery] string? status, [FromQuery] int page=1, [FromQuery] int pageSize=20,
    CancellationToken ct = default)
{
    var userIdStr = User.FindFirst("uid")?.Value;
    if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

    var query = Ctx.WorkflowInstances
        .AsNoTracking()
        .Where(i => i.StartedBy == userId);

    if (!string.IsNullOrWhiteSpace(status))
        query = query.Where(i => i.Status == status);

    var total = await query.CountAsync(ct);
    var items = await query
        .OrderByDescending(i => i.StartedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(i => new {
            i.InstanceId, i.DocumentId, i.Status,
            i.StartedAt, i.CompletedAt, i.Priority,
            i.DefinitionId, i.StartedBy
        })
        .ToListAsync(ct);

    return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
}

/// <summary>Get full workflow instance details with tasks and history.</summary>
[HttpGet("instances/{instanceId:int}")]
public async Task<ActionResult<ApiResponse<object>>> GetInstance(
    int instanceId, CancellationToken ct)
{
    var instance = await Ctx.WorkflowInstances
        .AsNoTracking()
        .FirstOrDefaultAsync(i => i.InstanceId == instanceId, ct);

    if (instance is null) return NotFound(ApiResponse<object>.Fail("سير العمل غير موجود"));

    var tasks = await Ctx.WorkflowTasks
        .AsNoTracking()
        .Where(t => t.InstanceId == instanceId)
        .Select(t => new {
            t.TaskId, t.StepId, t.Status, t.AssignedToUserId,
            t.DueAt, t.CompletedAt, t.CompletedBy, t.IsOverdue
        })
        .ToListAsync(ct);

    var history = await Ctx.AuditLogs
        .AsNoTracking()
        .Where(a => a.EntityType == "WorkflowInstance" && a.EntityId == instanceId.ToString())
        .OrderBy(a => a.CreatedAt)
        .Select(a => new { a.EventType, a.CreatedAt, a.UserId, a.AdditionalInfo })
        .ToListAsync(ct);

    return Ok(ApiResponse<object>.Ok(new { instance, tasks, history }));
}

/// <summary>Cancel an active workflow — removes all pending tasks.</summary>
[HttpPost("instances/{instanceId:int}/cancel")]
public async Task<ActionResult<ApiResponse<bool>>> CancelWorkflow(
    int instanceId, [FromBody] CancelWorkflowRequest req, CancellationToken ct)
{
    var userIdStr = User.FindFirst("uid")?.Value;
    if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

    var instance = await Ctx.WorkflowInstances.FindAsync(new object[]{instanceId}, ct);
    if (instance is null) return NotFound(ApiResponse<bool>.Fail("سير العمل غير موجود"));

    instance.Cancel(userId, req.Reason);

    // Remove all pending tasks
    var pendingTasks = await Ctx.WorkflowTasks
        .Where(t => t.InstanceId == instanceId && t.Status == "Pending")
        .ToListAsync(ct);
    Ctx.WorkflowTasks.RemoveRange(pendingTasks);

    Ctx.AuditLogs.Add(AuditLog.Create("WorkflowCancelled", "WorkflowInstance",
        instanceId.ToString(), userId, additionalInfo: req.Reason));

    await Ctx.SaveChangesAsync(ct);
    return Ok(ApiResponse<bool>.Ok(true));
}

/// <summary>Delete a completed workflow.</summary>
[HttpDelete("instances/{instanceId:int}")]
public async Task<ActionResult<ApiResponse<bool>>> DeleteWorkflow(
    int instanceId, CancellationToken ct)
{
    var userIdStr = User.FindFirst("uid")?.Value;
    if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

    var instance = await Ctx.WorkflowInstances.FindAsync(new object[]{instanceId}, ct);
    if (instance is null) return NotFound(ApiResponse<bool>.Fail("سير العمل غير موجود"));
    if (instance.Status != "Completed")
        return BadRequest(ApiResponse<bool>.Fail("يمكن حذف سير العمل المكتمل فقط"));

    Ctx.WorkflowInstances.Remove(instance);
    Ctx.AuditLogs.Add(AuditLog.Create("WorkflowDeleted", "WorkflowInstance",
        instanceId.ToString(), userId));
    await Ctx.SaveChangesAsync(ct);
    return Ok(ApiResponse<bool>.Ok(true));
}

/// <summary>Claim a pooled task from a group.</summary>
[HttpPost("tasks/{taskId:int}/claim")]
public async Task<ActionResult<ApiResponse<bool>>> ClaimTask(
    int taskId, CancellationToken ct)
{
    var userIdStr = User.FindFirst("uid")?.Value;
    if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

    var task = await Ctx.WorkflowTasks.FindAsync(new object[]{taskId}, ct);
    if (task is null) return NotFound();

    task.ClaimBy(userId);
    Ctx.AuditLogs.Add(AuditLog.Create("TaskClaimed", "WorkflowTask",
        taskId.ToString(), userId));
    await Ctx.SaveChangesAsync(ct);
    return Ok(ApiResponse<bool>.Ok(true));
}

/// <summary>Return task back to group pool.</summary>
[HttpPost("tasks/{taskId:int}/return-to-group")]
public async Task<ActionResult<ApiResponse<bool>>> ReturnToGroup(
    int taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
{
    var userIdStr = User.FindFirst("uid")?.Value;
    if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

    var task = await Ctx.WorkflowTasks.FindAsync(new object[]{taskId}, ct);
    if (task is null) return NotFound();

    task.ReturnToGroup(userId, req.Comment);
    Ctx.AuditLogs.Add(AuditLog.Create("TaskReturnedToGroup", "WorkflowTask",
        taskId.ToString(), userId));
    await Ctx.SaveChangesAsync(ct);
    return Ok(ApiResponse<bool>.Ok(true));
}

/// <summary>Submit multiple documents to the same workflow.</summary>
[HttpPost("bulk-submit")]
public async Task<ActionResult<ApiResponse<object>>> BulkSubmit(
    [FromBody] BulkSubmitRequest req, CancellationToken ct)
{
    var userIdStr = User.FindFirst("uid")?.Value;
    if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

    var results = new List<object>();
    foreach (var docId in req.DocumentIds)
    {
        try
        {
            var r = await _mediator.Send(new SubmitToWorkflowCommand(
                docId, req.WorkflowDefinitionId, req.Priority, req.Message), ct);
            results.Add(new { documentId=docId, success=r.Success, instanceId=r.Data?.InstanceId, error=r.Message });
        }
        catch (Exception ex)
        {
            results.Add(new { documentId=docId, success=false, error=ex.Message });
        }
    }

    return Ok(ApiResponse<object>.Ok(new {
        submitted = results.Count(r => ((dynamic)r).success),
        failed    = results.Count(r => !((dynamic)r).success),
        results
    }));
}

// ── Private DB access ──────────────────────────────────────────────────────────
private Darah.ECM.Infrastructure.Persistence.EcmDbContext Ctx =>
    HttpContext.RequestServices
        .GetRequiredService<Darah.ECM.Infrastructure.Persistence.EcmDbContext>();

public sealed record CancelWorkflowRequest(string? Reason = null);
public sealed record BulkSubmitRequest(
    Guid[] DocumentIds, int? WorkflowDefinitionId, int Priority=2, string? Message=null);
