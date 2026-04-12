using Darah.ECM.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Darah.ECM.Infrastructure.Security;

/// <summary>
/// Reads the current authenticated user from JWT claims injected via IHttpContextAccessor.
/// </summary>
public sealed class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public int    UserId    => int.TryParse(User?.FindFirstValue("uid"), out var id) ? id : 0;
    public string Username  => User?.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
    public string Email     => User?.FindFirstValue(ClaimTypes.Email) ?? "";
    public string FullNameAr => User?.FindFirstValue("name_ar") ?? Username;
    public string? FullNameEn => User?.FindFirstValue("name_en");
    public string Language  => User?.FindFirstValue("lang") ?? "ar";
    public string? IPAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? SessionId => User?.FindFirstValue("sid");
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IEnumerable<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value)
        ?? Enumerable.Empty<string>();

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
