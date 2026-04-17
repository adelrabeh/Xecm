using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.API.Controllers.v1;

/// <summary>Platform management: partitions, OAuth clients, system config, recycle bin, groups.</summary>
[ApiController]
[Route("api/v1/platform")]
[Authorize]
[Produces("application/json")]
public sealed class PlatformController : ControllerBase
{
    private readonly EcmDbContext _db;
    public PlatformController(EcmDbContext db) => _db = db;

    // ── PARTITIONS ─────────────────────────────────────────────────────────────
    [HttpGet("partitions")]
    public async Task<IActionResult> GetPartitions(CancellationToken ct)
    {
        var list = await _db.Partitions.AsNoTracking()
            .Select(p => new { p.PartitionId, p.Code, p.NameAr, p.NameEn, p.AuthHandler, p.IsActive })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(list));
    }

    [HttpPost("partitions")]
    public async Task<IActionResult> CreatePartition([FromBody] CreatePartitionRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var p = Partition.Create(req.Code, req.NameAr, userId, req.NameEn, req.AuthHandler ?? "Local");
        _db.Partitions.Add(p);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { p.PartitionId, p.Code, message = "تم إنشاء القسم بنجاح" }));
    }

    // ── OAUTH CLIENTS ──────────────────────────────────────────────────────────
    [HttpGet("oauth-clients")]
    public async Task<IActionResult> GetOAuthClients(CancellationToken ct)
    {
        var list = await _db.OAuthClients.AsNoTracking()
            .Select(c => new { c.ClientId, c.ClientKey, c.Name, c.Scopes, c.IsActive, c.CreatedAt })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(list));
    }

    [HttpPost("oauth-clients")]
    public async Task<IActionResult> CreateOAuthClient([FromBody] CreateOAuthClientRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var client = OAuthClient.Create(req.Name, req.Scopes, userId);
        _db.OAuthClients.Add(client);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {
            client.ClientId, client.ClientKey, client.ClientSecret,
            client.Name, client.Scopes,
            message = "⚠️ احتفظ بالـ Client Secret الآن — لن يُعرض مرة أخرى"
        }));
    }

    [HttpPost("oauth-clients/{id:int}/rotate")]
    public async Task<IActionResult> RotateSecret(int id, CancellationToken ct)
    {
        var client = await _db.OAuthClients.FindAsync(new object[]{id}, ct);
        if (client is null) return NotFound();
        client.Rotate();
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { client.ClientKey, client.ClientSecret,
            message = "تم تجديد الـ Secret بنجاح" }));
    }

    // ── SYSTEM CONFIG ──────────────────────────────────────────────────────────
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig([FromQuery] string? category, CancellationToken ct)
    {
        var query = _db.SystemConfigs.AsNoTracking().Where(c => c.PartitionId == null);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);
        var list = await query.OrderBy(c => c.Category).ThenBy(c => c.Key).ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(list));
    }

    [HttpPut("config/{key}")]
    public async Task<IActionResult> UpdateConfig(string key, [FromBody] UpdateConfigRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key, ct);
        if (config is null)
        {
            var newCfg = SystemConfig.Create(key, req.Value, req.Category ?? "General", userId);
            _db.SystemConfigs.Add(newCfg);
        }
        else config.Update(req.Value, userId);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ── RECYCLE BIN ────────────────────────────────────────────────────────────
    [HttpGet("recycle-bin")]
    public async Task<IActionResult> GetRecycleBin(CancellationToken ct)
    {
        var list = await _db.RecycleBin.AsNoTracking()
            .Where(e => !e.IsPermanent && e.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(list));
    }

    [HttpPost("recycle-bin/{entryId:long}/restore")]
    public async Task<IActionResult> Restore(long entryId, CancellationToken ct)
    {
        var entry = await _db.RecycleBin.FindAsync(new object[]{entryId}, ct);
        if (entry is null) return NotFound();

        var doc = await _db.Documents.FindAsync(new object[]{entry.DocumentId}, ct);
        if (doc is not null) doc.Restore(int.Parse(User.FindFirst("uid")?.Value ?? "1"));

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpDelete("recycle-bin/{entryId:long}")]
    public async Task<IActionResult> PermanentDelete(long entryId, CancellationToken ct)
    {
        var entry = await _db.RecycleBin.FindAsync(new object[]{entryId}, ct);
        if (entry is null) return NotFound();
        entry.MarkPermanent();
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ── USER GROUPS ────────────────────────────────────────────────────────────
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups(CancellationToken ct)
    {
        var groups = await _db.UserGroups.AsNoTracking()
            .Where(g => g.IsActive)
            .Select(g => new { g.GroupId, g.Code, g.NameAr, g.NameEn, g.IsDynamic })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(groups));
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var g = UserGroup.Create(req.Code, req.NameAr, userId, isDynamic: req.IsDynamic);
        _db.UserGroups.Add(g);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { g.GroupId, g.Code, message = "تم إنشاء المجموعة بنجاح" }));
    }

    [HttpPost("groups/{groupId:int}/members")]
    public async Task<IActionResult> AddMember(int groupId, [FromBody] AddMemberRequest req, CancellationToken ct)
    {
        _db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = req.UserId, IsManager = req.IsManager });
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ── APPLICATION ROLES ──────────────────────────────────────────────────────
    [HttpGet("app-roles")]
    public async Task<IActionResult> GetAppRoles(CancellationToken ct)
    {
        var roles = await _db.ApplicationRoles.AsNoTracking()
            .Select(r => new { r.AppRoleId, r.Code, r.NameAr, r.NameEn, r.IsSystem })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(roles));
    }
}

// ── Request models ─────────────────────────────────────────────────────────────
public sealed record CreatePartitionRequest(string Code, string NameAr, string? NameEn = null, string? AuthHandler = null);
public sealed record CreateOAuthClientRequest(string Name, string Scopes);
public sealed record UpdateConfigRequest(string Value, string? Category = null);
public sealed record CreateGroupRequest(string Code, string NameAr, bool IsDynamic = false);
public sealed record AddMemberRequest(int UserId, bool IsManager = false);
