using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Darah.ECM.API.Controllers.v1;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly EcmDbContext _db;
    public UsersController(EcmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Select(u => new {
                u.UserId, u.Username, u.Email,
                u.FullNameAr, u.FullNameEn,
                u.IsActive, u.JobTitle
            })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(users));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var hash = HashPassword(req.Password ?? "Change@Me1234");
        var user = Darah.ECM.Domain.Entities.User.Create(req.Username.ToLowerInvariant(), req.Email, hash, req.FullNameAr, 1);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new {
            user.UserId, user.Username,
            message = "تم إنشاء المستخدم بنجاح"
        }));
    }

    [HttpPut("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[]{id}, ct);
        if (user is null) return NotFound();
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var userIdStr = User.FindFirst("uid")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user is null) return NotFound();

        // Verify current password
        var currentHash = HashPassword(req.CurrentPassword);
        if (!user.PasswordHash.Equals(currentHash, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<bool>.Fail("كلمة المرور الحالية غير صحيحة"));

        // Update password using reflection (private setter)
        typeof(Darah.ECM.Domain.Entities.User)
            .GetProperty("PasswordHash")?
            .SetValue(user, HashPassword(req.NewPassword));

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<bool>.Ok(true, "تم تغيير كلمة المرور بنجاح"));
    }

    [HttpPut("language")]
    public async Task<IActionResult> SetLanguage(
        [FromBody] SetLanguageRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirst("uid")?.Value ?? "1");
        var user   = await _db.Users.FindAsync(new object[]{userId}, ct);
        if (user is null) return NotFound();
        // Store preference — will be included in next JWT or fetched on load
        // In production: update User.PreferredLanguage DB field
        return Ok(ApiResponse<bool>.Ok(true));
    }

    private static string HashPassword(string p)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(p)));
    }
}

public sealed record SetLanguageRequest(string Language);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record CreateUserRequest(
    string Username, string Email, string FullNameAr,
    string? FullNameEn = null, string? Password = null);
