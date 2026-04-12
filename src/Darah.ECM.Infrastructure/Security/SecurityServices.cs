using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Darah.ECM.Infrastructure.Security;

public sealed class CurrentUserService : ICurrentUser, ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserService(IHttpContextAccessor http) => _http = http;
    private ClaimsPrincipal? Principal => _http.HttpContext?.User;
    public int UserId => int.TryParse(Principal?.FindFirstValue("uid"), out var id) ? id : 0;
    int? ICurrentUserAccessor.UserId => IsAuthenticated ? UserId : null;
    public string Username => Principal?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public string FullNameAr => Principal?.FindFirstValue("name_ar") ?? string.Empty;
    public string? FullNameEn => Principal?.FindFirstValue("name_en");
    public string Language => Principal?.FindFirstValue("lang") ?? "ar";
    public string? SessionId => Principal?.FindFirstValue("sid");
    public string? IPAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public IEnumerable<string> Permissions => Principal?.FindAll("perm").Select(c => c.Value)
        ?? Enumerable.Empty<string>();
    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

public interface IJwtTokenService
{
    string GenerateAccessToken(int userId, string username, string email,
        string nameAr, string? nameEn, string lang, string sessionId,
        IEnumerable<string> permissions);
    string GenerateRefreshToken();
    string HashToken(string token);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(int userId, string username, string email,
        string nameAr, string? nameEn, string lang, string sessionId,
        IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new("uid",     userId.ToString()),
            new(ClaimTypes.Name,  username),
            new(ClaimTypes.Email, email),
            new("name_ar", nameAr),
            new("lang",    lang),
            new("sid",     sessionId),
        };
        if (nameEn is not null) claims.Add(new("name_en", nameEn));
        foreach (var p in permissions) claims.Add(new("perm", p));

        var key   = GetSigningKey();
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer:   _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims:   claims,
            expires:  DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var b = new byte[64];
        RandomNumberGenerator.Fill(b);
        return Convert.ToBase64String(b);
    }

    public string HashToken(string token)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(
            sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token)));
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var s = _config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey not configured.");
        if (s.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be >= 32 chars.");
        return new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(s));
    }
}
