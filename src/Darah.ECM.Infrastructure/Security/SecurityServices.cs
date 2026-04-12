using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Darah.ECM.Application.Common.Interfaces;
public sealed class CurrentUserService : ICurrentUser
{
    private readonly ClaimsPrincipal? _user;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _user = httpContextAccessor.HttpContext?.User;
    }

    public int UserId =>
        int.TryParse(_user?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    public string UserName =>
        _user?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

    public string Email =>
        _user?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

    public string DisplayNameAr =>
        _user?.FindFirst("display_name_ar")?.Value ?? string.Empty;

    public string DisplayNameEn =>
        _user?.FindFirst("display_name_en")?.Value ?? string.Empty;

    public string Department =>
        _user?.FindFirst("department")?.Value ?? string.Empty;

    public bool IsAuthenticated =>
        _user?.Identity?.IsAuthenticated ?? false;
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(
        int userId,
        string userName,
        string email,
        string? displayNameAr = null,
        string? displayNameEn = null,
        string? department = null,
        IEnumerable<string>? roles = null)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing");
        var issuer = _configuration["Jwt:Issuer"] ?? "Darah.ECM";
        var audience = _configuration["Jwt:Audience"] ?? "Darah.ECM.Users";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Email, email)
        };

        if (!string.IsNullOrWhiteSpace(displayNameAr))
            claims.Add(new Claim("display_name_ar", displayNameAr));

        if (!string.IsNullOrWhiteSpace(displayNameEn))
            claims.Add(new Claim("display_name_en", displayNameEn));

        if (!string.IsNullOrWhiteSpace(department))
            claims.Add(new Claim("department", department));

        if (roles != null)
        {
            foreach (var role in roles.Where(r => !string.IsNullOrWhiteSpace(r)))
                claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
