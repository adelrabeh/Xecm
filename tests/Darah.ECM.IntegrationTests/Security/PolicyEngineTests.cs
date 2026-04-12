using Darah.ECM.Application.Common.Guards;
using Darah.ECM.Infrastructure.Security.Abac;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Darah.ECM.IntegrationTests.Security;

public sealed class PolicyEngineTests
{
    private static PolicyEngine CreateEngine()
        => new PolicyEngine(new Mock<ILogger<PolicyEngine>>().Object);

    private static AccessRequest MakeRequest(
        string action,
        IEnumerable<string>? permissions = null,
        int classificationOrder = 2,
        bool isLegalHold = false) => new AccessRequest(
            UserId:                       1,
            UserPermissions:              permissions ?? new[] { action },
            UserRoleIds:                  Array.Empty<int>(),
            UserDepartmentId:             null,
            Action:                       action,
            ResourceType:                 "Document",
            ResourceId:                   Guid.NewGuid().ToString(),
            ResourceClassificationOrder:  classificationOrder,
            WorkspaceId:                  null,
            IsResourceOnLegalHold:        isLegalHold);

    // ── SystemAdmin bypass ────────────────────────────────────────────────────
    [Fact]
    public void SystemAdmin_GrantedEverything()
    {
        var engine = CreateEngine();
        var req = MakeRequest("documents.delete",
            permissions: new[] { "admin.system" }, classificationOrder: 4);
        var decision = engine.Evaluate(req);
        Assert.True(decision.IsGranted);
    }

    // ── Legal hold blocks writes ──────────────────────────────────────────────
    [Theory]
    [InlineData("documents.update")]
    [InlineData("documents.delete")]
    [InlineData("documents.checkout")]
    [InlineData("documents.checkin")]
    public void LegalHold_BlocksWriteActions(string action)
    {
        var engine = CreateEngine();
        var req = MakeRequest(action, permissions: new[] { action }, isLegalHold: true);
        var decision = engine.Evaluate(req);
        Assert.False(decision.IsGranted);
        Assert.Equal("LegalHoldPolicy", decision.DenyPolicy);
    }

    [Fact]
    public void LegalHold_AllowsReadActions()
    {
        var engine = CreateEngine();
        var req = MakeRequest("documents.read",
            permissions: new[] { "documents.read" }, isLegalHold: true);
        var decision = engine.Evaluate(req);
        Assert.True(decision.IsGranted); // Read is allowed even under legal hold
    }

    // ── Classification access control ──────────────────────────────────────────
    [Fact]
    public void Secret_Document_Blocked_WithoutSecretPermission()
    {
        var engine = CreateEngine();
        var req = MakeRequest("documents.read",
            permissions: new[] { "documents.read" }, classificationOrder: 4);
        var decision = engine.Evaluate(req);
        Assert.False(decision.IsGranted);
        Assert.Equal("ClassificationPolicy.Secret", decision.DenyPolicy);
    }

    [Fact]
    public void Secret_Document_Allowed_WithSecretPermission()
    {
        var engine = CreateEngine();
        var req = MakeRequest("documents.read",
            permissions: new[] { "documents.read", "documents.access.secret" },
            classificationOrder: 4);
        var decision = engine.Evaluate(req);
        Assert.True(decision.IsGranted);
    }

    [Fact]
    public void Confidential_Blocked_WithoutConfidentialPermission()
    {
        var engine = CreateEngine();
        var req = MakeRequest("documents.read",
            permissions: new[] { "documents.read" }, classificationOrder: 3);
        var decision = engine.Evaluate(req);
        Assert.False(decision.IsGranted);
        Assert.Equal("ClassificationPolicy.Confidential", decision.DenyPolicy);
    }

    [Fact]
    public void Internal_Allowed_WithStandardPermission()
    {
        var engine = CreateEngine();
        var req = MakeRequest("documents.read",
            permissions: new[] { "documents.read" }, classificationOrder: 2);
        var decision = engine.Evaluate(req);
        Assert.True(decision.IsGranted);
    }

    // ── RBAC permission check ─────────────────────────────────────────────────
    [Fact]
    public void MissingPermission_Denied()
    {
        var engine = CreateEngine();
        var req = MakeRequest("documents.delete",
            permissions: new[] { "documents.read" }); // only read, not delete
        var decision = engine.Evaluate(req);
        Assert.False(decision.IsGranted);
        Assert.Equal("RBACPolicy", decision.DenyPolicy);
    }

    [Fact]
    public void CorrectPermission_Granted()
    {
        var engine = CreateEngine();
        var req = MakeRequest("documents.download",
            permissions: new[] { "documents.download", "documents.read" },
            classificationOrder: 2);
        var decision = engine.Evaluate(req);
        Assert.True(decision.IsGranted);
    }
}
