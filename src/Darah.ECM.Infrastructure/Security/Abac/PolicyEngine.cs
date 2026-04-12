using Darah.ECM.Application.Common.Guards;
using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.Security.Abac;

/// <summary>
/// Centralized ABAC Policy Engine — implements Application.IPolicyEngine.
/// Evaluation order (deny-wins):
///   1. SystemAdmin → bypass all
///   2. LegalHold → block write actions
///   3. Classification clearance check
///   4. RBAC permission check
/// </summary>
public sealed class PolicyEngine : IPolicyEngine
{
    private readonly ILogger<PolicyEngine> _logger;
    public PolicyEngine(ILogger<PolicyEngine> logger) => _logger = logger;

    public AccessDecision Evaluate(AccessRequest request)
    {
        // Rule 1: SystemAdmin bypasses all
        if (request.UserPermissions.Contains("admin.system", StringComparer.OrdinalIgnoreCase))
            return new AccessDecision(true, "SystemAdmin: all access granted");

        // Rule 2: Legal hold blocks write actions
        if (request.IsResourceOnLegalHold && IsWriteAction(request.Action))
        {
            _logger.LogWarning("Access denied (LegalHold): User={User} Action={Action}",
                request.UserId, request.Action);
            return new AccessDecision(false, "الوصول مرفوض: الوثيقة خاضعة لتجميد قانوني", "LegalHoldPolicy");
        }

        // Rule 3: Classification clearance
        if (request.ResourceClassificationOrder.HasValue)
        {
            var classResult = EvaluateClassification(request);
            if (!classResult.IsGranted) return classResult;
        }

        // Rule 4: RBAC permission check
        if (!request.UserPermissions.Contains(request.Action, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Access denied (RBAC): User={User} lacks {Action}", request.UserId, request.Action);
            return new AccessDecision(false, $"ليس لديك صلاحية '{request.Action}'", "RBACPolicy");
        }

        return new AccessDecision(true, "Access granted");
    }

    private static AccessDecision EvaluateClassification(AccessRequest request)
    {
        var level = request.ResourceClassificationOrder!.Value;

        if (level >= 4 && !request.UserPermissions.Contains("documents.access.secret", StringComparer.OrdinalIgnoreCase))
            return new AccessDecision(false,
                "الوصول مرفوض: مستوى التصنيف (سري للغاية) يتطلب صلاحية خاصة",
                "ClassificationPolicy.Secret");

        if (level >= 3 && !request.UserPermissions.Any(p =>
                p.StartsWith("documents.access.confidential", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("documents.access.secret", StringComparison.OrdinalIgnoreCase)))
            return new AccessDecision(false,
                "الوصول مرفوض: مستوى التصنيف (سري) يتطلب صلاحية وصول مناسبة",
                "ClassificationPolicy.Confidential");

        return new AccessDecision(true, "Classification OK");
    }

    private static bool IsWriteAction(string action) =>
        action.Contains(".update", StringComparison.OrdinalIgnoreCase) ||
        action.Contains(".delete", StringComparison.OrdinalIgnoreCase) ||
        action.Contains(".create", StringComparison.OrdinalIgnoreCase) ||
        action.Contains(".checkin", StringComparison.OrdinalIgnoreCase) ||
        action.Contains(".checkout", StringComparison.OrdinalIgnoreCase);
}
