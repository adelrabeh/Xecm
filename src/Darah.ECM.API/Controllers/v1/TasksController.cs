using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>Document-centric task management API.</summary>
[ApiController]
[Route("api/v1/tasks")]
[Authorize]
[Produces("application/json")]
public sealed class TasksController : ControllerBase
{
    private readonly EcmDbContext _db;

    public TasksController(EcmDbContext db) => _db = db;

    // ── GET all tasks for current user ─────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetMyTasks(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userIdStr = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var query = _db.DocumentTasks.AsNoTracking()
            .Where(t => t.AssignedToUserId == userId || t.CreatedBy == userId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(t => t.Priority == priority);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new {
                t.TaskId, t.TraceId, t.Title, t.TaskType,
                t.Status, t.Priority, t.RoutingType,
                t.DocumentId, t.DueDate, t.CreatedAt,
                t.AssignedToUserId, t.ApplicationRole,
                IsOverdue = t.DueDate.HasValue && t.DueDate < DateTime.UtcNow && t.Status == "Open"
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    // ── GET task by id ─────────────────────────────────────────────────────────
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var task = await _db.DocumentTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TaskId == id, ct);

        if (task is null) return NotFound(ApiResponse<object>.Fail("المهمة غير موجودة"));

        var comments = await _db.TaskComments
            .AsNoTracking()
            .Where(c => c.TaskId == id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.CommentId, c.Body, c.IsInternal, c.CreatedAt, c.CreatedBy })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { task, comments }));
    }

    // ── CREATE task (optionally linked to document) ────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest req, CancellationToken ct)
    {
        var userIdStr = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var task = DocumentTask.Create(
            req.Title, userId, req.DocumentId, req.TaskType ?? "Review",
            req.Priority ?? "Normal", req.DueDate, req.AssignedToUserId);

        _db.DocumentTasks.Add(task);
        await _db.SaveChangesAsync(ct);

        // Audit log
        _db.AuditLogs.Add(AuditLog.Create("TaskCreated", "DocumentTask",
            task.TaskId.ToString(), userId, additionalInfo: $"TraceId:{task.TraceId}"));
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new {
            task.TaskId, task.TraceId, task.Title, task.Status,
            message = "تم إنشاء المهمة بنجاح"
        }));
    }

    // ── ASSIGN task ────────────────────────────────────────────────────────────
    [HttpPost("{id:long}/assign")]
    public async Task<IActionResult> Assign(long id, [FromBody] AssignTaskRequest req, CancellationToken ct)
    {
        var userIdStr = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var task = await _db.DocumentTasks.FindAsync(new object[]{id}, ct);
        if (task is null) return NotFound();

        task.Assign(req.ToUserId, req.ApplicationRole);
        _db.AuditLogs.Add(AuditLog.Create("TaskAssigned", "DocumentTask",
            id.ToString(), userId, additionalInfo: $"TraceId:{task.TraceId}"));
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ── COMPLETE task (approve/reject/edit/comment) ────────────────────────────
    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteTaskRequest req, CancellationToken ct)
    {
        var userIdStr = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var task = await _db.DocumentTasks.FindAsync(new object[]{id}, ct);
        if (task is null) return NotFound();

        task.Complete(userId, req.Resolution, req.Note);

        // Add comment if provided
        if (!string.IsNullOrWhiteSpace(req.Note))
        {
            _db.TaskComments.Add(TaskComment.Create(id, req.Note, userId));
        }

        _db.AuditLogs.Add(AuditLog.Create($"Task{req.Resolution}", "DocumentTask",
            id.ToString(), userId, additionalInfo: $"TraceId:{task.TraceId}"));
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ── REASSIGN task ──────────────────────────────────────────────────────────
    [HttpPost("{id:long}/reassign")]
    public async Task<IActionResult> Reassign(long id, [FromBody] ReassignTaskRequest req, CancellationToken ct)
    {
        var userIdStr = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var task = await _db.DocumentTasks.FindAsync(new object[]{id}, ct);
        if (task is null) return NotFound();

        task.Reassign(req.ToUserId, req.Instructions);
        _db.AuditLogs.Add(AuditLog.Create("TaskReassigned", "DocumentTask",
            id.ToString(), userId, additionalInfo: $"TraceId:{task.TraceId}"));
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ── ADD comment ────────────────────────────────────────────────────────────
    [HttpPost("{id:long}/comments")]
    public async Task<IActionResult> AddComment(long id, [FromBody] AddCommentRequest req, CancellationToken ct)
    {
        var userIdStr = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var comment = TaskComment.Create(id, req.Body, userId, req.IsInternal);
        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { comment.CommentId, comment.Body }));
    }

    // ── GET tasks for a document ───────────────────────────────────────────────
    [HttpGet("by-document/{documentId:guid}")]
    public async Task<IActionResult> GetByDocument(Guid documentId, CancellationToken ct)
    {
        var tasks = await _db.DocumentTasks
            .AsNoTracking()
            .Where(t => t.DocumentId == documentId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new {
                t.TaskId, t.TraceId, t.Title, t.TaskType,
                t.Status, t.Priority, t.AssignedToUserId,
                t.DueDate, t.CreatedAt, t.CompletedAt, t.Resolution
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(tasks));
    }

    // ── Dashboard summary ──────────────────────────────────────────────────────
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var userIdStr = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var summary = new
        {
            open     = await _db.DocumentTasks.CountAsync(t => t.AssignedToUserId == userId && t.Status == "Open", ct),
            overdue  = await _db.DocumentTasks.CountAsync(t => t.AssignedToUserId == userId && t.Status == "Open" && t.DueDate < DateTime.UtcNow, ct),
            done     = await _db.DocumentTasks.CountAsync(t => t.AssignedToUserId == userId && t.Status == "Done", ct),
            total    = await _db.DocumentTasks.CountAsync(t => t.AssignedToUserId == userId, ct),
        };

        return Ok(ApiResponse<object>.Ok(summary));
    }
}

// ── Request models ─────────────────────────────────────────────────────────────
public sealed record CreateTaskRequest(
    string Title, Guid? DocumentId = null, string? TaskType = null,
    string? Priority = null, DateTime? DueDate = null, int? AssignedToUserId = null);

public sealed record AssignTaskRequest(int ToUserId, string? ApplicationRole = null);

public sealed record CompleteTaskRequest(string Resolution, string? Note = null);

public sealed record ReassignTaskRequest(int ToUserId, string? Instructions = null);

public sealed record AddCommentRequest(string Body, bool IsInternal = false);
