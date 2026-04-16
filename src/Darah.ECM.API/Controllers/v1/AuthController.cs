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
        var result = await _mediator.Send(cmd, ct);
        if (!result.Success)
            return Unauthorized(result);

        var user = result.Data!;
        var token = GenerateToken(user);
        var refreshToken = GenerateRefreshToken();

        return Ok(ApiResponse<LoginResultDto>.Ok(new LoginResultDto(
            token, refreshToken,
            user.UserId, user.Username, user.FullNameAr, user.FullNameEn,
            user.Email, user.Language, user.Permissions,
            DateTime.UtcNow.AddHours(8))));
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

    // ─── Token generation ──────────────────────────────────────────────────────
    private string GenerateToken(AuthenticatedUserDto user)
    {
        var claims = new List<Claim>
        {
            new("uid", user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("name_ar", user.FullNameAr),
            new("lang", user.Language),
            new("sid", Guid.NewGuid().ToString()),
        };
        if (user.FullNameEn is not null) claims.Add(new("name_en", user.FullNameEn));
        foreach (var p in user.Permissions) claims.Add(new("perm", p));

        var secret = _config["Jwt:SecretKey"] ?? "DarahECM2026SuperSecretKey32chars!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "darah-ecm",
            audience: _config["Jwt:Audience"] ?? "darah-ecm-users",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var b = new byte[64];
        RandomNumberGenerator.Fill(b);
        return Convert.ToBase64String(b);
    }
}

public sealed record LoginResultDto(
    string Token, string RefreshToken,
    int UserId, string Username, string FullNameAr, string? FullNameEn,
    string Email, string Language, IEnumerable<string> Permissions,
    DateTime ExpiresAt);
