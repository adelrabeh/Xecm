using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>Hierarchy-aware, permission-based task escalation API.</summary>
[ApiController]
[Route("api/v1/escalations")]
[Authorize]
[Produces("application/json")]
public sealed class EscalationController : ControllerBase
{
    private readonly EcmDbContext _db;
    public EscalationController(EcmDbContext db) => _db = db;

    // ── GET: schema (roles, rules) ────────────────────────────────────────────
    [HttpGet("schema")]
    [AllowAnonymous]
    public IActionResult GetSchema() => Ok(ApiResponse<object>.Ok(new
    {
        roles = new[]
        {
            new { id=(int)UserRole.Viewer,            code="Viewer",            nameAr="مشاهد",       level=0, canEscalate=false },
            new { id=(int)UserRole.Employee,          code="Employee",          nameAr="موظف",         level=1, canEscalate=true  },
            new { id=(int)UserRole.Supervisor,        code="Supervisor",        nameAr="مشرف",         level=2, canEscalate=true  },
            new { id=(int)UserRole.DepartmentManager, code="DepartmentManager", nameAr="مدير القسم",   level=3, canEscalate=true  },
            new { id=(int)UserRole.SystemAdmin,       code="SystemAdmin",       nameAr="مدير النظام",  level=4, canEscalate=false },
        },
        escalationPaths = new[]
        {
            new { from="موظف",       to="مشرف",        level=1, rule="نفس القسم فقط" },
            new { from="مشرف",       to="مدير القسم",  level=2, rule="نفس القسم فقط" },
            new { from="مدير القسم", to="مدير قسم آخر",level=3, rule="عبر الأقسام"   },
        },
        levels = new[]
        {
            new { level=1, nameAr="إلى المشرف",       code="ToSupervisor"        },
            new { level=2, nameAr="إلى مدير القسم",  code="ToDepartmentManager" },
            new { level=3, nameAr="تصعيد متقاطع",    code="ToCrossManager"      },
        },
        statuses = new[]
        {
            new { id=0, nameAr="معلق",    code="Pending"  },
            new { id=1, nameAr="مقبول",   code="Accepted" },
            new { id=2, nameAr="مرفوض",   code="Rejected" },
            new { id=3, nameAr="محلول",   code="Resolved" },
            new { id=4, nameAr="ملغى",    code="Cancelled"},
        },
    }));

    // ── POST: validate escalation before allowing it ──────────────────────────
    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ValidateEscalationRequest req)
    {
        var fromRole = (UserRole)req.FromRole;
        var toRole   = (UserRole)req.ToRole;
        var canEscalate = EscalationPolicy.CanEscalate(fromRole, toRole, req.SameDepartment);

        return Ok(ApiResponse<object>.Ok(new
        {
            allowed = canEscalate,
            level   = canEscalate ? (int)EscalationPolicy.GetLevel(fromRole) : 0,
            reason  = canEscalate ? null : EscalationPolicy.GetDenialReason(fromRole, toRole),
        }));
    }

    // ── POST: create escalation ───────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEscalationRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var fromRole = (UserRole)req.FromRole;
        var toRole   = (UserRole)req.ToRole;
        var samedept = req.FromDepartment?.Equals(req.ToDepartment, StringComparison.OrdinalIgnoreCase) == true;

        // Permission check
        if (!EscalationPolicy.CanEscalate(fromRole, toRole, samedept))
        {
            return BadRequest(ApiResponse<bool>.Fail(
                EscalationPolicy.GetDenialReason(fromRole, toRole)));
        }

        var escalation = TaskEscalation.Create(
            req.TaskId, userId, req.ToUserId,
            toRole, EscalationPolicy.GetLevel(fromRole),
            req.Reason, req.FromDepartment);

        _db.TaskEscalations.Add(escalation);

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            EntityName   = "TaskEscalation",
            EntityId     = req.TaskId.ToString(),
            Action       = "Escalate",
            PerformedBy  = userId,
            PerformedAt  = DateTime.UtcNow,
            OldValues    = null,
            NewValues    = $"Escalated to userId={req.ToUserId}, role={toRole}, level={(int)EscalationPolicy.GetLevel(fromRole)}",
            IpAddress    = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            escalation.EscalationId,
            escalation.EscalationLevel,
            escalation.EscalatedToRole,
            message = $"تم التصعيد بنجاح — المستوى {(int)escalation.EscalationLevel}"
        }));
    }

    // ── GET: escalations assigned to me ──────────────────────────────────────
    [HttpGet("assigned-to-me")]
    public async Task<IActionResult> AssignedToMe(CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var list = await _db.TaskEscalations.AsNoTracking()
            .Where(e => e.EscalatedToUserId == userId)
            .OrderByDescending(e => e.EscalatedAt)
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(list));
    }

    // ── GET: escalation stats (dashboard) ─────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var all = await _db.TaskEscalations.AsNoTracking().ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            total      = all.Count,
            pending    = all.Count(e => e.Status == EscalationStatus.Pending),
            accepted   = all.Count(e => e.Status == EscalationStatus.Accepted),
            resolved   = all.Count(e => e.Status == EscalationStatus.Resolved),
            byLevel    = new[]
            {
                new { level=1, nameAr="إلى المشرف",      count=all.Count(e=>e.EscalationLevel==EscalationLevel.ToSupervisor) },
                new { level=2, nameAr="إلى مدير القسم", count=all.Count(e=>e.EscalationLevel==EscalationLevel.ToDepartmentManager) },
                new { level=3, nameAr="تصعيد متقاطع",   count=all.Count(e=>e.EscalationLevel==EscalationLevel.ToCrossManager) },
            },
            byDept = all.GroupBy(e => e.Department ?? "غير محدد")
                .Select(g => new { dept=g.Key, count=g.Count() }),
        }));
    }

    // ── PUT: accept/reject/resolve ─────────────────────────────────────────────
    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var e = await _db.TaskEscalations.FindAsync(new object[]{id}, ct);
        if (e is null) return NotFound();
        e.Accept(userId);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] ResolutionRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var e = await _db.TaskEscalations.FindAsync(new object[]{id}, ct);
        if (e is null) return NotFound();
        e.Reject(userId, req.Note ?? "");
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("{id:long}/resolve")]
    public async Task<IActionResult> Resolve(long id, [FromBody] ResolutionRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var e = await _db.TaskEscalations.FindAsync(new object[]{id}, ct);
        if (e is null) return NotFound();
        e.Resolve(userId, req.Note ?? "");
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }
}

public sealed record ValidateEscalationRequest(int FromRole, int ToRole, bool SameDepartment, string? FromDepartment=null, string? ToDepartment=null);
public sealed record CreateEscalationRequest(int TaskId, int ToUserId, int FromRole, int ToRole, string? Reason=null, string? FromDepartment=null, string? ToDepartment=null);
public sealed record ResolutionRequest(string? Note=null);
