using Darah.ECM.Application.Auth;
using Darah.ECM.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Darah.ECM.API.Controllers.v1;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;

    // SHA256 of "Admin@2026"
    private const string ADMIN_HASH = "A36AEF5A11C4073FBE60314FC9DF530A9D5F986533594D1F5190742FF9E0E408";

    public AuthController(IMediator mediator, IConfiguration config)
    {
        _mediator = mediator;
        _config = config;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand cmd, CancellationToken ct)
    {
        // Try DB first
        try
        {
            var result = await _mediator.Send(cmd, ct);
            if (result.Success && result.Data != null)
            {
                var u = result.Data;
                var token = MakeToken(u.UserId, u.Username, u.FullNameAr,
                    u.FullNameEn, u.Email, u.Language, u.Permissions);
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        token,
                        refreshToken = MakeRefreshToken(),
                        userId = u.UserId,
                        username = u.Username,
                        fullNameAr = u.FullNameAr,
                        fullNameEn = u.FullNameEn,
                        email = u.Email,
                        language = u.Language,
                        permissions = u.Permissions,
                        expiresAt = DateTime.UtcNow.AddHours(8)
                    }
                });
            }
        }
        catch { /* fall through to hardcoded admin */ }

        // Hardcoded admin fallback (works without DB)
        if (IsAdmin(cmd.Username, cmd.Password))
        {
            var token = MakeToken(1, "admin", "مدير النظام", "System Admin",
                "admin@darah.gov.sa", "ar",
                new[] { "admin.*", "documents.*", "workflow.*", "audit.*", "records.*" });
            return Ok(new
            {
                success = true,
                data = new
                {
                    token,
                    refreshToken = MakeRefreshToken(),
                    userId = 1,
                    username = "admin",
                    fullNameAr = "مدير النظام",
                    fullNameEn = "System Admin",
                    email = "admin@darah.gov.sa",
                    language = "ar",
                    permissions = new[] { "admin.*", "documents.*", "workflow.*", "audit.*", "records.*" },
                    expiresAt = DateTime.UtcNow.AddHours(8)
                }
            });
        }

        return Unauthorized(new { success = false, message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        success = true,
        data = new
        {
            userId = User.FindFirst("uid")?.Value,
            username = User.FindFirst(ClaimTypes.Name)?.Value,
            fullNameAr = User.FindFirst("name_ar")?.Value
        }
    });

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => Ok(new { success = true, data = true });

    private static bool IsAdmin(string username, string password)
    {
        if (!username.Equals("admin", StringComparison.OrdinalIgnoreCase)) return false;
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return hash.Equals(ADMIN_HASH, StringComparison.OrdinalIgnoreCase);
    }

    private string MakeToken(int userId, string username, string nameAr,
        string? nameEn, string email, string lang, IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new("uid", userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email),
            new("name_ar", nameAr),
            new("lang", lang),
            new("sid", Guid.NewGuid().ToString()),
        };
        if (nameEn != null) claims.Add(new("name_en", nameEn));
        foreach (var p in permissions) claims.Add(new("perm", p));

        var secret = _config["Jwt:SecretKey"] ?? "DarahECM2026SuperSecretKey32chars!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "darah-ecm",
            audience: _config["Jwt:Audience"] ?? "darah-ecm-users",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string MakeRefreshToken()
    {
        var b = new byte[64];
        RandomNumberGenerator.Fill(b);
        return Convert.ToBase64String(b);
    }
}
