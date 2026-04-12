namespace Darah.ECM.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(
        int userId,
        string userName,
        string email,
        string? displayNameAr = null,
        string? displayNameEn = null,
        string? department = null,
        IEnumerable<string>? roles = null);
}