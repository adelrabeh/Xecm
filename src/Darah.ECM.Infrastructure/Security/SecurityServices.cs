using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    public IEnumerable<string> Permissions => Principal?.FindAll("perm").Select(c => c.Value) ?? Enumerable.Empty<string>();
    public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

public interface IJwtTokenService
{
    string GenerateAccessToken(int userId, string username, string email, string nameAr, string? nameEn, string lang, string sessionId, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    string HashToken(string token);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(int userId, string username, string email, string nameAr, string? nameEn, string lang, string sessionId, IEnumerable<string> permissions)
    {
        var key = GetSigningKey();
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "15");
        var claims = new List<Claim>
        {
            new("uid", userId.ToString()), new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email), new("name_ar", nameAr),
            new("lang", lang), new("sid", sessionId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (!string.IsNullOrEmpty(nameEn)) claims.Add(new("name_en", nameEn));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));
        var token = new JwtSecurityToken(issuer: _config["Jwt:Issuer"], audience: _config["Jwt:Audience"], claims: claims, expires: DateTime.UtcNow.AddMinutes(expiry), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken() { var b = new byte[64]; RandomNumberGenerator.Fill(b); return Convert.ToBase64String(b); }
    public string HashToken(string token) { using var sha = SHA256.Create(); return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token))); }
    private SymmetricSecurityKey GetSigningKey()
    {
        var s = _config["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey not configured.");
        if (s.Length < 32) throw new InvalidOperationException("Jwt:SecretKey must be >= 32 chars.");
        return new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(s));
    }
}

namespace Darah.ECM.Infrastructure.Logging
{
    public sealed class AuditService : IAuditService
    {
        private readonly EcmDbContext _ctx;
        private readonly ICurrentUser _user;
        private readonly ILogger<AuditService> _log;
        public AuditService(EcmDbContext ctx, ICurrentUser user, ILogger<AuditService> log) { _ctx = ctx; _user = user; _log = log; }
    
        public async Task LogAsync(string eventType, string? entityType = null, string? entityId = null, object? oldValues = null, object? newValues = null, string severity = "Info", bool isSuccessful = true, string? failureReason = null, string? additionalInfo = null, CancellationToken ct = default)
        {
            try
            {
                var log = AuditLog.Create(eventType, entityType, entityId,
                    _user.IsAuthenticated ? _user.UserId : null,
                    _user.IsAuthenticated ? _user.Username : null,
                    _user.IPAddress,
                    oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                    newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
                    additionalInfo, severity, isSuccessful, failureReason, _user.SessionId);
                _ctx.AuditLogs.Add(log);
                await _ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex) { _log.LogError(ex, "Audit log failed for {EventType}", eventType); }
        }
    }
}
