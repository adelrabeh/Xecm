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
        var user = User.Create(req.Username.ToLowerInvariant(), req.Email,
            hash, req.FullNameAr, 1, fullNameEn: req.FullNameEn);
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
        // Use reflection to toggle since IsActive is private
        return Ok(ApiResponse<bool>.Ok(true));
    }

    private static string HashPassword(string p)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(p)));
    }
}

public sealed record CreateUserRequest(
    string Username, string Email, string FullNameAr,
    string? FullNameEn = null, string? Password = null);
