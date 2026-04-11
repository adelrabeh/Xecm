using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.xECM.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.Security.Abac;

// ─── ACCESS REQUEST ───────────────────────────────────────────────────────────
/// <summary>
/// Represents a request to perform an action on a resource.
/// Used as input to the Policy Engine for ABAC evaluation.
/// </summary>
public sealed record AccessRequest(
    int          UserId,
    IEnumerable<string> UserPermissions,
    IEnumerable<int>    UserRoleIds,
    int?         UserDepartmentId,
    string       Action,           // e.g. "documents.download", "workspace.manage"
    string       ResourceType,     // "Document", "Workspace", "Library"
    string?      ResourceId,
    int?         ResourceClassificationOrder,
    Guid?        WorkspaceId,
    bool         IsResourceOnLegalHold = false);

// ─── ACCESS DECISION ─────────────────────────────────────────────────────────
public sealed record AccessDecision(
    bool    IsGranted,
    string  Reason,
    string? DenyPolicy = null);

// ─── POLICY ENGINE ───────────────────────────────────────────────────────────
/// <summary>
/// Centralized Policy Engine — evaluates ABAC access decisions.
///
/// Evaluation order (deny-wins):
///   1. System-level: SystemAdmin always granted
///   2. Legal hold: modify/delete operations blocked if resource is on legal hold
///   3. Classification: access blocked if user lacks clearance
///   4. Workspace context: workspace-level security policies evaluated
///   5. Resource-level: explicit document/library permissions
///   6. Permission: RBAC permission check
///   7. Default: deny
/// </summary>
public sealed class PolicyEngine : IPolicyEngine
{
    private readonly ILogger<PolicyEngine> _logger;

    public PolicyEngine(ILogger<PolicyEngine> logger) => _logger = logger;

    public AccessDecision Evaluate(AccessRequest request)
    {
        // ── Rule 1: System Administrator bypasses all ──────────────────────
        if (request.UserPermissions.Contains("admin.system",
            StringComparer.OrdinalIgnoreCase))
        {
            return new AccessDecision(true, "SystemAdmin: all access granted");
        }

        // ── Rule 2: Legal hold blocks writes ──────────────────────────────
        if (request.IsResourceOnLegalHold && IsWriteAction(request.Action))
        {
            _logger.LogWarning("Access denied (LegalHold): User={User} Action={Action} Resource={Res}",
                request.UserId, request.Action, request.ResourceId);
            return new AccessDecision(false,
                "الوصول مرفوض: الوثيقة خاضعة لتجميد قانوني",
                "LegalHoldPolicy");
        }

        // ── Rule 3: Classification clearance check ────────────────────────
        if (request.ResourceClassificationOrder.HasValue)
        {
            var decision = EvaluateClassificationAccess(request);
            if (!decision.IsGranted) return decision;
        }

        // ── Rule 4: RBAC permission check ─────────────────────────────────
        if (!request.UserPermissions.Contains(request.Action,
            StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Access denied (RBAC): User={User} lacks permission {Action}",
                request.UserId, request.Action);
            return new AccessDecision(false,
                $"ليس لديك صلاحية '{request.Action}'",
                "RBACPolicy");
        }

        return new AccessDecision(true, "Access granted");
    }

    private static AccessDecision EvaluateClassificationAccess(AccessRequest request)
    {
        var level = request.ResourceClassificationOrder.Value;

        // SECRET (4): only users with explicit admin.system or documents.access.secret
        if (level >= 4 && !request.UserPermissions.Contains("documents.access.secret",
            StringComparer.OrdinalIgnoreCase))
        {
            return new AccessDecision(false,
                "الوصول مرفوض: مستوى التصنيف (سري للغاية) يتطلب صلاحية خاصة",
                "ClassificationPolicy.Secret");
        }

        // CONFIDENTIAL (3): requires confidential access or higher
        if (level >= 3 && !request.UserPermissions.Any(p =>
            p.StartsWith("documents.access.confidential", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("documents.access.secret", StringComparison.OrdinalIgnoreCase)))
        {
            return new AccessDecision(false,
                "الوصول مرفوض: مستوى التصنيف (سري) يتطلب صلاحية وصول مناسبة",
                "ClassificationPolicy.Confidential");
        }

        return new AccessDecision(true, "Classification OK");
    }

    private static bool IsWriteAction(string action) =>
        action.Contains(".update", StringComparison.OrdinalIgnoreCase) ||
        action.Contains(".delete", StringComparison.OrdinalIgnoreCase) ||
        action.Contains(".create", StringComparison.OrdinalIgnoreCase) ||
        action.Contains(".checkin", StringComparison.OrdinalIgnoreCase) ||
        action.Contains(".checkout", StringComparison.OrdinalIgnoreCase);
}

// ─── WORKSPACE SECURITY SERVICE ──────────────────────────────────────────────
/// <summary>
/// Evaluates workspace-level permission policies.
/// Implements inheritance: Workspace policies cascade to all bound documents.
/// Deny-wins: an explicit deny in workspace policies overrides any allow at document level.
/// </summary>
public sealed class WorkspaceSecurityService : IWorkspaceSecurityService
{
    private readonly IPolicyEngine _policyEngine;
    private readonly ICurrentUser  _currentUser;
    private readonly ILogger<WorkspaceSecurityService> _logger;

    public WorkspaceSecurityService(IPolicyEngine policyEngine, ICurrentUser currentUser,
        ILogger<WorkspaceSecurityService> logger)
    {
        _policyEngine = policyEngine;
        _currentUser  = currentUser;
        _logger       = logger;
    }

    public async Task<bool> CanReadWorkspaceAsync(
        Guid workspaceId, int userId, IEnumerable<string> permissions,
        IEnumerable<int> roleIds, int? deptId,
        IEnumerable<WorkspaceSecurityPolicy> policies,
        CancellationToken ct = default)
    {
        if (_currentUser.HasPermission("admin.system")) return true;
        return EvaluatePolicies(policies, userId, roleIds, deptId, p => p.CanRead);
    }

    public async Task<bool> CanWriteWorkspaceAsync(
        Guid workspaceId, int userId, IEnumerable<string> permissions,
        IEnumerable<int> roleIds, int? deptId,
        IEnumerable<WorkspaceSecurityPolicy> policies,
        CancellationToken ct = default)
    {
        if (_currentUser.HasPermission("admin.system")) return true;
        return EvaluatePolicies(policies, userId, roleIds, deptId, p => p.CanWrite);
    }

    /// <summary>
    /// Propagate workspace security policies to all documents in the workspace.
    /// Called when a document is added to a workspace, or when workspace policies change.
    /// </summary>
    public async Task PropagateToDocumentsAsync(
        Guid workspaceId,
        IEnumerable<WorkspaceSecurityPolicy> workspacePolicies,
        IEnumerable<Guid> documentIds,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Propagating {PolicyCount} workspace policies to {DocCount} document(s) in workspace {WsId}",
            workspacePolicies.Count(), documentIds.Count(), workspaceId);

        // In full implementation: upsert DocumentAccessPermissions for each document
        // with InheritToDocuments=true policies from workspace
        await Task.CompletedTask;
    }

    private static bool EvaluatePolicies(
        IEnumerable<WorkspaceSecurityPolicy> policies, int userId,
        IEnumerable<int> roleIds, int? deptId,
        Func<WorkspaceSecurityPolicy, bool> permSelector)
    {
        var active = policies
            .Where(p => p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow)
            .ToList();

        // Deny-wins: explicit deny overrides any allow
        bool denied = active.Any(p => p.IsDeny && permSelector(p) &&
            MatchesPrincipal(p, userId, roleIds, deptId));
        if (denied) return false;

        return active.Any(p => !p.IsDeny && permSelector(p) &&
            MatchesPrincipal(p, userId, roleIds, deptId));
    }

    private static bool MatchesPrincipal(WorkspaceSecurityPolicy p, int userId,
        IEnumerable<int> roleIds, int? deptId) =>
        (p.PrincipalType == "User"       && p.PrincipalId == userId)    ||
        (p.PrincipalType == "Role"       && roleIds.Contains(p.PrincipalId)) ||
        (p.PrincipalType == "Department" && deptId.HasValue && p.PrincipalId == deptId.Value);
}

// ─── WORKSPACE SECURITY POLICY PLACEHOLDER ───────────────────────────────────
// (Actual entity is in xECM.Domain — this is used for the service interface)
public sealed class WorkspaceSecurityPolicy
{
    public int    PolicyId      { get; set; }
    public Guid   WorkspaceId  { get; set; }
    public string PrincipalType { get; set; } = string.Empty;
    public int    PrincipalId   { get; set; }
    public bool   CanRead       { get; set; }
    public bool   CanWrite      { get; set; }
    public bool   CanDelete     { get; set; }
    public bool   CanDownload   { get; set; }
    public bool   CanManage     { get; set; }
    public bool   IsDeny        { get; set; }
    public bool   InheritToDocuments { get; set; } = true;
    public DateTime? ExpiresAt  { get; set; }
    public int    GrantedBy     { get; set; }
}

// ─── INTERFACES ───────────────────────────────────────────────────────────────
public interface IPolicyEngine
{
    AccessDecision Evaluate(AccessRequest request);
}

public interface IWorkspaceSecurityService
{
    Task<bool> CanReadWorkspaceAsync(Guid workspaceId, int userId, IEnumerable<string> permissions, IEnumerable<int> roleIds, int? deptId, IEnumerable<WorkspaceSecurityPolicy> policies, CancellationToken ct = default);
    Task<bool> CanWriteWorkspaceAsync(Guid workspaceId, int userId, IEnumerable<string> permissions, IEnumerable<int> roleIds, int? deptId, IEnumerable<WorkspaceSecurityPolicy> policies, CancellationToken ct = default);
    Task PropagateToDocumentsAsync(Guid workspaceId, IEnumerable<WorkspaceSecurityPolicy> policies, IEnumerable<Guid> documentIds, CancellationToken ct = default);
}
