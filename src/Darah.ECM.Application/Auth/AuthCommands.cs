using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Darah.ECM.Application.Auth;

public sealed record LoginCommand(string Username, string Password)
    : IRequest<ApiResponse<LoginResultDto>>;

public sealed record LoginResultDto(
    string Token,
    string RefreshToken,
    int UserId,
    string Username,
    string FullNameAr,
    string? FullNameEn,
    string Email,
    string Language,
    IEnumerable<string> Permissions,
    DateTime ExpiresAt);

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, ApiResponse<LoginResultDto>>
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _config;

    public LoginCommandHandler(IUserRepository users, IConfiguration config)
    {
        _users = users;
        _config = config;
    }

    public async Task<ApiResponse<LoginResultDto>> Handle(
        LoginCommand cmd, CancellationToken ct)
    {
        var user = await _users.GetByUsernameAsync(cmd.Username, ct);

        if (user is null || !user.IsActive)
            return ApiResponse<LoginResultDto>.Fail("اسم المستخدم أو كلمة المرور غير صحيحة");

        if (user.IsLocked)
            return ApiResponse<LoginResultDto>.Fail("الحساب مقفل. تواصل مع المسؤول");

        if (!VerifyPassword(cmd.Password, user.PasswordHash))
            return ApiResponse<LoginResultDto>.Fail("اسم المستخدم أو كلمة المرور غير صحيحة");

        var permissions = await _users.GetPermissionsAsync(user.UserId, ct);
        var token = GenerateToken(user.UserId, user.Username, user.Email,
            user.FullNameAr, user.FullNameEn, user.LanguagePreference, permissions);
        var refreshToken = GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddHours(8);

        return ApiResponse<LoginResultDto>.Ok(new LoginResultDto(
            token, refreshToken,
            user.UserId, user.Username, user.FullNameAr, user.FullNameEn,
            user.Email, user.LanguagePreference, permissions, expiresAt));
    }

    private static bool VerifyPassword(string password, string hash)
    {
        // SHA256 hash comparison for seeded users
        using var sha = SHA256.Create();
        var computed = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return computed.Equals(hash, StringComparison.OrdinalIgnoreCase);
    }

    private string GenerateToken(int userId, string username, string email,
        string nameAr, string? nameEn, string lang, IEnumerable<string> permissions)
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
