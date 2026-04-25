using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>Retention enforcement — compliance with NCA/DGA standards.</summary>
[ApiController]
[Route("api/v1/retention")]
[Authorize]
[Produces("application/json")]
public sealed class RetentionController : ControllerBase
{
    private readonly EcmDbContext _db;
    public RetentionController(EcmDbContext db) => _db = db;

    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] string? status = null,
        [FromQuery] bool expiredOnly = false,
        CancellationToken ct = default)
    {
        var q = _db.RetentionSchedules.AsNoTracking();
        if (expiredOnly) q = q.Where(e => DateTime.UtcNow >= e.ExpiresAt && !e.IsLegalHold);

        var list = await q.OrderBy(e => e.ExpiresAt).ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new {
            total       = list.Count,
            expired     = list.Count(e => e.IsExpired()),
            dueSoon90   = list.Count(e => e.IsDueSoon(90) && !e.IsExpired()),
            legalHold   = list.Count(e => e.IsLegalHold),
            items       = list,
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRetentionRequest req, CancellationToken ct)
    {
        var entry = RetentionScheduleEntry.Create(
            req.RecordId, req.RecordTitle, req.RetentionLabel,
            req.RetentionYears, req.Department, req.RecordType);

        _db.RetentionSchedules.Add(entry);
        _db.AuditLogs.Add(new AuditLog {
            EntityName="RetentionSchedule", EntityId=req.RecordId.ToString(),
            Action="CreateSchedule", PerformedBy=int.Parse(User.FindFirst("uid")?.Value??"1"),
            PerformedAt=DateTime.UtcNow, NewValues=$"Retention={req.RetentionYears}y expires={DateTime.UtcNow.AddYears(req.RetentionYears):d}",
        });
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true, "تم تسجيل جدول الاحتفاظ"));
    }

    [HttpPost("{id:long}/approve-disposal")]
    public async Task<IActionResult> ApproveDisposal(long id, [FromBody] ReviewRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var e = await _db.RetentionSchedules.FindAsync(new object[]{id}, ct);
        if (e is null) return NotFound();
        if (!e.IsExpired() && !e.IsDueSoon(30))
            return BadRequest(ApiResponse<bool>.Fail("السجل لم يبلغ موعد المراجعة بعد"));
        e.ApproveDisposal(userId, req.Note ?? "");
        _db.AuditLogs.Add(new AuditLog {
            EntityName="RetentionSchedule", EntityId=id.ToString(),
            Action="ApproveDisposal", PerformedBy=userId, PerformedAt=DateTime.UtcNow,
            NewValues=$"ApprovedDisposal note={req.Note}",
        });
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true, "تم اعتماد الإتلاف — يجب تنفيذه خلال 30 يوماً"));
    }

    [HttpPost("{id:long}/extend")]
    public async Task<IActionResult> ExtendRetention(long id, [FromBody] ExtendRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var e = await _db.RetentionSchedules.FindAsync(new object[]{id}, ct);
        if (e is null) return NotFound();
        e.ExtendRetention(req.Years, userId, req.Note ?? "");
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true, $"تم تمديد الاحتفاظ {req.Years} سنوات"));
    }

    [HttpPost("{id:long}/legal-hold")]
    public async Task<IActionResult> ToggleLegalHold(long id, CancellationToken ct)
    {
        var e = await _db.RetentionSchedules.FindAsync(new object[]{id}, ct);
        if (e is null) return NotFound();
        if (e.IsLegalHold) e.RemoveLegalHold(); else e.PlaceLegalHold();
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var all = await _db.RetentionSchedules.AsNoTracking().ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {
            total     = all.Count,
            expired   = all.Count(e => e.IsExpired()),
            dueSoon   = all.Count(e => e.IsDueSoon(90) && !e.IsExpired()),
            legalHold = all.Count(e => e.IsLegalHold),
            byDept    = all.GroupBy(e => e.Department ?? "غير محدد")
                .Select(g => new { dept=g.Key, total=g.Count(), expired=g.Count(e=>e.IsExpired()) }),
        }));
    }
}

public sealed record CreateRetentionRequest(long RecordId, string RecordTitle, string RetentionLabel, int RetentionYears, string? Department, string? RecordType);
public sealed record ReviewRequest(string? Note);
public sealed record ExtendRequest(int Years, string? Note);
