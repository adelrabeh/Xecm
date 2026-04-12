using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Application.Notifications;
using Darah.ECM.Domain.Interfaces.Services;

namespace Darah.ECM.Application.Common.Guards;

// ─── POLICY ENGINE ABSTRACTION (in Application - no Infrastructure dependency) ─
public sealed record AccessRequest(
    int UserId, IEnumerable<string> UserPermissions, IEnumerable<int> UserRoleIds,
    int? UserDepartmentId, string Action, string ResourceType, string? ResourceId,
    int? ResourceClassificationOrder, Guid? WorkspaceId,
    bool IsResourceOnLegalHold = false);

public sealed record AccessDecision(bool IsGranted, string Reason, string? DenyPolicy = null);

public interface IPolicyEngine
{
    AccessDecision Evaluate(AccessRequest request);
}

// ─── AUTHORIZATION RESULT ─────────────────────────────────────────────────────
public sealed class AuthorizationResult
{
    public bool   IsGranted  { get; private set; }
    public string? DenyReason { get; private set; }
    public string? DenyPolicy { get; private set; }

    public static AuthorizationResult Granted() => new() { IsGranted = true };
    public static AuthorizationResult Denied(string reason, string? policy = null) =>
        new() { IsGranted = false, DenyReason = reason, DenyPolicy = policy };

    public ApiResponse<T> ToFailResponse<T>() =>
        ApiResponse<T>.Unauthorized(DenyReason ?? "غير مصرح بالوصول");
}

// ─── AUTHORIZATION GUARD INTERFACE ───────────────────────────────────────────
public interface IAuthorizationGuard
{
    Task<AuthorizationResult> AuthorizeDocumentAsync(
        Guid documentId, string action, Document? document = null, CancellationToken ct = default);
    Task<AuthorizationResult> AuthorizeWorkflowActionAsync(
        int taskId, string action, WorkflowTask? task = null, CancellationToken ct = default);
    Task<AuthorizationResult> AuthorizeFolderAsync(
        int folderId, string action, CancellationToken ct = default);
    Task<AuthorizationResult> AuthorizeAdminActionAsync(
        string action, CancellationToken ct = default);
}

// ─── CONCRETE GUARD (uses IPolicyEngine - resolved at runtime via DI) ─────────
public sealed class AuthorizationGuard : IAuthorizationGuard
{
    private readonly IPolicyEngine   _policyEngine;
    private readonly ICurrentUser    _currentUser;
    private readonly IUserRepository _userRepo;

    public AuthorizationGuard(IPolicyEngine policyEngine, ICurrentUser currentUser,
        IUserRepository userRepo)
        { _policyEngine = policyEngine; _currentUser = currentUser; _userRepo = userRepo; }

    public async Task<AuthorizationResult> AuthorizeDocumentAsync(
        Guid documentId, string action, Document? document = null, CancellationToken ct = default)
    {
        var (perms, roles, dept) = await GetContextAsync(ct);
        var decision = _policyEngine.Evaluate(new AccessRequest(
            _currentUser.UserId, perms, roles, dept, action, "Document",
            documentId.ToString(), document?.Classification.Order,
            document?.PrimaryWorkspaceId, document?.IsLegalHold ?? false));
        return decision.IsGranted
            ? AuthorizationResult.Granted()
            : AuthorizationResult.Denied(decision.Reason, decision.DenyPolicy);
    }

    public async Task<AuthorizationResult> AuthorizeWorkflowActionAsync(
        int taskId, string action, WorkflowTask? task = null, CancellationToken ct = default)
    {
        var (perms, roles, _) = await GetContextAsync(ct);
        if (task is not null)
        {
            bool assigned = task.AssignedToUserId == _currentUser.UserId;
            bool inRole   = task.AssignedToRoleId.HasValue && roles.Contains(task.AssignedToRoleId.Value);
            if (!assigned && !inRole)
                return AuthorizationResult.Denied("المهمة غير معينة لك", "WorkflowAssignmentPolicy");
        }
        var decision = _policyEngine.Evaluate(new AccessRequest(
            _currentUser.UserId, perms, roles, null, $"workflow.{action.ToLower()}",
            "WorkflowTask", taskId.ToString(), null, null));
        return decision.IsGranted
            ? AuthorizationResult.Granted()
            : AuthorizationResult.Denied(decision.Reason, decision.DenyPolicy);
    }

    public async Task<AuthorizationResult> AuthorizeFolderAsync(
        int folderId, string action, CancellationToken ct = default)
    {
        var (perms, roles, dept) = await GetContextAsync(ct);
        var decision = _policyEngine.Evaluate(new AccessRequest(
            _currentUser.UserId, perms, roles, dept, action, "Folder",
            folderId.ToString(), null, null));
        return decision.IsGranted
            ? AuthorizationResult.Granted()
            : AuthorizationResult.Denied(decision.Reason, decision.DenyPolicy);
    }

    public async Task<AuthorizationResult> AuthorizeAdminActionAsync(
        string action, CancellationToken ct = default)
    {
        var (perms, roles, _) = await GetContextAsync(ct);
        var decision = _policyEngine.Evaluate(new AccessRequest(
            _currentUser.UserId, perms, roles, null, action, "System", null, null, null));
        return decision.IsGranted
            ? AuthorizationResult.Granted()
            : AuthorizationResult.Denied(decision.Reason, decision.DenyPolicy);
    }

    private async Task<(IEnumerable<string> perms, IEnumerable<int> roles, int? dept)>
        GetContextAsync(CancellationToken ct)
    {
        var perms = _currentUser.Permissions.Any()
            ? _currentUser.Permissions
            : await _userRepo.GetPermissionsAsync(_currentUser.UserId, ct);
        var roles = await _userRepo.GetRoleIdsAsync(_currentUser.UserId, ct);
        var dept  = await _userRepo.GetDepartmentIdAsync(_currentUser.UserId, ct);
        return (perms, roles, dept);
    }
}
