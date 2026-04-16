using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Interfaces.Repositories;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace Darah.ECM.Application.Auth;

// ─── Command ──────────────────────────────────────────────────────────────────
public sealed record LoginCommand(string Username, string Password)
    : IRequest<ApiResponse<AuthenticatedUserDto>>;

// ─── Result DTO (no token — token generated in API layer) ─────────────────────
public sealed record AuthenticatedUserDto(
    int UserId,
    string Username,
    string FullNameAr,
    string? FullNameEn,
    string Email,
    string Language,
    IEnumerable<string> Permissions);

// ─── Handler ──────────────────────────────────────────────────────────────────
public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, ApiResponse<AuthenticatedUserDto>>
{
    private readonly IUserRepository _users;

    public LoginCommandHandler(IUserRepository users) => _users = users;

    public async Task<ApiResponse<AuthenticatedUserDto>> Handle(
        LoginCommand cmd, CancellationToken ct)
    {
        var user = await _users.GetByUsernameAsync(cmd.Username, ct);

        if (user is null || !user.IsActive)
            return ApiResponse<AuthenticatedUserDto>.Fail(
                "اسم المستخدم أو كلمة المرور غير صحيحة");

        if (user.IsLocked)
            return ApiResponse<AuthenticatedUserDto>.Fail(
                "الحساب مقفل. تواصل مع مسؤول النظام");

        if (!VerifyPassword(cmd.Password, user.PasswordHash))
            return ApiResponse<AuthenticatedUserDto>.Fail(
                "اسم المستخدم أو كلمة المرور غير صحيحة");

        var permissions = await _users.GetPermissionsAsync(user.UserId, ct);

        return ApiResponse<AuthenticatedUserDto>.Ok(new AuthenticatedUserDto(
            user.UserId, user.Username, user.FullNameAr, user.FullNameEn,
            user.Email, user.LanguagePreference, permissions));
    }

    internal static bool VerifyPassword(string password, string hash)
    {
        using var sha = SHA256.Create();
        var computed = Convert.ToHexString(
            sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return computed.Equals(hash, StringComparison.OrdinalIgnoreCase);
    }
}
