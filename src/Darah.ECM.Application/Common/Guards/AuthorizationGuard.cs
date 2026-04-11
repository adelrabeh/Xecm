using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Infrastructure.Security.Abac;

namespace Darah.ECM.Application.Common.Guards;

/// <summary>
/// Centralized authorization guard — single evaluation pipeline for all modules.
///
/// PROBLEM SOLVED:
///   Before this guard, permission checks were scattered:
///     - DocumentsController.cs: [RequirePermission("documents.read")]
///     - WorkflowCommandHandler: manual check in handler body
///     - RecordsCommands: no explicit check (relied on controller attribute)
///     - PolicyEngine.Evaluate() called inconsistently
///
/// SOLUTION:
///   All handlers inject IAuthorizationGuard and call a single method.
///   The guard aggregates:
///     1. PolicyEngine (ABAC) evaluation
///     2. Document-specific resource checks (classification, legal hold)
///     3. Workspace context checks (if applicable)
///     4. Produces a consistent AuthorizationResult with audit data
///
/// PATTERN: Application service — knows about domain entities and policy engine,
///           but has no infrastructure dependencies (no DB, no HTTP).
/// </summary>
public interface IAuthorizationGuard
{
    Task<AuthorizationResult> AuthorizeDocumentAsync(
        Guid documentId, string action,
        Document? document = null,
        CancellationToken ct = default);

    Task<AuthorizationResult> AuthorizeWorkflowActionAsync(
        int taskId, string action,
        WorkflowTask? task = null,
        CancellationToken ct = default);

    Task<AuthorizationResult> AuthorizeFolderAsync(
        int folderId, string action,
        CancellationToken ct = default);

    Task<AuthorizationResult> AuthorizeAdminActionAsync(
        string action, CancellationToken ct = default);
}

public sealed class AuthorizationResult
{
    public bool    IsGranted    { get; private set; }
    public string? DenyReason   { get; private set; }
    public string? DenyPolicy   { get; private set; }

    public static AuthorizationResult Granted() =>
        new() { IsGranted = true };

    public static AuthorizationResult Denied(string reason, string? policy = null) =>
        new() { IsGranted = false, DenyReason = reason, DenyPolicy = policy };

    public ApiResponse<T> ToFailResponse<T>() =>
        ApiResponse<T>.Unauthorized(DenyReason ?? "غير مصرح بالوصول");
}

/// <summary>
/// Concrete implementation — wires PolicyEngine + current user context.
/// </summary>
public sealed class AuthorizationGuard : IAuthorizationGuard
{
    private readonly IPolicyEngine _policyEngine;
    private readonly ICurrentUser  _currentUser;
    private readonly IUserRepository _userRepo;

    public AuthorizationGuard(IPolicyEngine policyEngine, ICurrentUser currentUser,
        IUserRepository userRepo)
    {
        _policyEngine = policyEngine;
        _currentUser  = currentUser;
        _userRepo     = userRepo;
    }

    public async Task<AuthorizationResult> AuthorizeDocumentAsync(
        Guid documentId, string action,
        Document? document = null,
        CancellationToken ct = default)
    {
        var (permissions, roleIds, deptId) = await GetUserContextAsync(ct);

        var request = new AccessRequest(
            UserId:                      _currentUser.UserId,
            UserPermissions:             permissions,
            UserRoleIds:                 roleIds,
            UserDepartmentId:            deptId,
            Action:                      action,
            ResourceType:                "Document",
            ResourceId:                  documentId.ToString(),
            ResourceClassificationOrder: document?.Classification.Order,
            WorkspaceId:                 document?.PrimaryWorkspaceId,
            IsResourceOnLegalHold:       document?.IsLegalHold ?? false);

        var decision = _policyEngine.Evaluate(request);
        return decision.IsGranted
            ? AuthorizationResult.Granted()
            : AuthorizationResult.Denied(decision.Reason, decision.DenyPolicy);
    }

    public async Task<AuthorizationResult> AuthorizeWorkflowActionAsync(
        int taskId, string action,
        WorkflowTask? task = null,
        CancellationToken ct = default)
    {
        var (permissions, roleIds, _) = await GetUserContextAsync(ct);

        // Workflow-specific check: user must be the assigned user or in the assigned role
        if (task is not null)
        {
            bool isAssignedUser = task.AssignedToUserId == _currentUser.UserId;
            bool isInRole = task.AssignedToRoleId.HasValue &&
                roleIds.Contains(task.AssignedToRoleId.Value);

            if (!isAssignedUser && !isInRole)
                return AuthorizationResult.Denied(
                    "المهمة غير معينة لك. لا يمكنك تنفيذ هذا الإجراء.", "WorkflowAssignmentPolicy");
        }

        var request = new AccessRequest(
            UserId:                      _currentUser.UserId,
            UserPermissions:             permissions,
            UserRoleIds:                 roleIds,
            UserDepartmentId:            null,
            Action:                      $"workflow.{action.ToLower()}",
            ResourceType:                "WorkflowTask",
            ResourceId:                  taskId.ToString(),
            ResourceClassificationOrder: null,
            WorkspaceId:                 null);

        var decision = _policyEngine.Evaluate(request);
        return decision.IsGranted
            ? AuthorizationResult.Granted()
            : AuthorizationResult.Denied(decision.Reason, decision.DenyPolicy);
    }

    public async Task<AuthorizationResult> AuthorizeFolderAsync(
        int folderId, string action, CancellationToken ct = default)
    {
        var (permissions, roleIds, deptId) = await GetUserContextAsync(ct);
        var request = new AccessRequest(
            UserId:                      _currentUser.UserId,
            UserPermissions:             permissions,
            UserRoleIds:                 roleIds,
            UserDepartmentId:            deptId,
            Action:                      action,
            ResourceType:                "Folder",
            ResourceId:                  folderId.ToString(),
            ResourceClassificationOrder: null,
            WorkspaceId:                 null);

        var decision = _policyEngine.Evaluate(request);
        return decision.IsGranted
            ? AuthorizationResult.Granted()
            : AuthorizationResult.Denied(decision.Reason, decision.DenyPolicy);
    }

    public async Task<AuthorizationResult> AuthorizeAdminActionAsync(
        string action, CancellationToken ct = default)
    {
        var (permissions, roleIds, _) = await GetUserContextAsync(ct);
        var request = new AccessRequest(
            UserId:                      _currentUser.UserId,
            UserPermissions:             permissions,
            UserRoleIds:                 roleIds,
            UserDepartmentId:            null,
            Action:                      action,
            ResourceType:                "System",
            ResourceId:                  null,
            ResourceClassificationOrder: null,
            WorkspaceId:                 null);

        var decision = _policyEngine.Evaluate(request);
        return decision.IsGranted
            ? AuthorizationResult.Granted()
            : AuthorizationResult.Denied(decision.Reason, decision.DenyPolicy);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────
    private async Task<(IEnumerable<string> permissions, IEnumerable<int> roleIds, int? deptId)>
        GetUserContextAsync(CancellationToken ct)
    {
        // Use cached permissions from ICurrentUser (already loaded from JWT claims)
        // Fall back to DB if claims are empty (e.g., service-to-service calls)
        var permissions = _currentUser.Permissions.Any()
            ? _currentUser.Permissions
            : await _userRepo.GetPermissionsAsync(_currentUser.UserId, ct);

        var roleIds = await _userRepo.GetRoleIdsAsync(_currentUser.UserId, ct);
        var deptId  = await _userRepo.GetDepartmentIdAsync(_currentUser.UserId, ct);

        return (permissions, roleIds, deptId);
    }
}
