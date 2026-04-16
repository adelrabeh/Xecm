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

    // Hardcoded admin for when DB is not available (InMemory mode)
    // Password: Admin@2026 → SHA256
    private const string ADMIN_HASH = "A36AEF5A11C4073FBE60314FC9DF530A9D5F986533594D1F5190742FF9E0E408";

    public AuthController(IMediator mediator, IConfiguration config)
    {
        _mediator = mediator;
        _config = config;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login(
        [FromBody] LoginCommand cmd, CancellationToken ct)
    {
        // Try DB first
        var result = await _mediator.Send(cmd, ct);

        if (result.Success)
        {
            var user = result.Data!;
            var token = GenerateToken(user.UserId, user.Username, user.FullNameAr,
                user.FullNameEn, user.Email, user.Language, user.Permissions);
            return Ok(BuildLoginResponse(token, user.UserId, user.Username,
                user.FullNameAr, user.FullNameEn, user.Email, user.Language, user.Permissions));
        }

        // Fallback: check hardcoded admin (for InMemory/dev mode)
        if (IsAdminCredentials(cmd.Username, cmd.Password))
        {
            var token = GenerateToken(1, "admin", "مدير النظام", "System Admin",
                "admin@darah.gov.sa", "ar",
                new[] { "documents.read", "documents.write", "admin.*", "workflow.*" });
            return Ok(BuildLoginResponse(token, 1, "admin", "مدير النظام",
                "System Admin", "admin@darah.gov.sa", "ar",
                new[] { "documents.read", "documents.write", "admin.*", "workflow.*" }));
        }

        return Unauthorized(ApiResponse<LoginResultDto>.Fail("اسم المستخدم أو كلمة المرور غير صحيحة"));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<ApiResponse<object>> Me()
    {
        var userId = User.FindFirst("uid")?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var nameAr = User.FindFirst("name_ar")?.Value;
        return Ok(ApiResponse<object>.Ok(new { userId, username, nameAr }));
    }

    [HttpPost("logout")]
    [Authorize]
    public ActionResult<ApiResponse<bool>> Logout()
        => Ok(ApiResponse<bool>.Ok(true));

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsAdminCredentials(string username, string password)
    {
        if (!username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return false;
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return hash.Equals(ADMIN_HASH, StringComparison.OrdinalIgnoreCase);
    }

    private static ActionResult<ApiResponse<LoginResultDto>> BuildLoginResponse(
        string token, int userId, string username, string nameAr, string? nameEn,
        string email, string lang, IEnumerable<string> permissions)
    {
        return new OkObjectResult(ApiResponse<LoginResultDto>.Ok(new LoginResultDto(
            token, Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            userId, username, nameAr, nameEn, email, lang, permissions,
            DateTime.UtcNow.AddHours(8))));
    }

    private string GenerateToken(int userId, string username, string nameAr,
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
        if (nameEn is not null) claims.Add(new("name_en", nameEn));
        foreach (var p in permissions) claims.Add(new("perm", p));

        var secret = _config["Jwt:SecretKey"] ?? "DarahECM2026SuperSecretKey32chars!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "darah-ecm",
            audience: _config["Jwt:Audience"] ?? "darah-ecm-users",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }
}

public sealed record LoginResultDto(
    string Token, string RefreshToken,
    int UserId, string Username, string FullNameAr, string? FullNameEn,
    string Email, string Language, IEnumerable<string> Permissions,
    DateTime ExpiresAt);
