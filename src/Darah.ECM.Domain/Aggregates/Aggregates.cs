using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Events;
using Darah.ECM.Domain.ValueObjects;

namespace Darah.ECM.Domain.Aggregates;

/// <summary>
/// DocumentAggregate — the consistency boundary for document operations.
/// Enforces all business rules around a document and its versions, tags, and metadata.
/// </summary>
public class DocumentAggregate : IAggregateRoot
{
    public Document Document { get; private set; }
    public IReadOnlyList<DocumentVersion> Versions { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; }

    private DocumentAggregate(Document document,
        IReadOnlyList<DocumentVersion> versions,
        IReadOnlyList<string> tags)
    {
        Document = document;
        Versions = versions;
        Tags = tags;
    }

    /// <summary>Reconstruct aggregate from persisted state (used by repositories).</summary>
    public static DocumentAggregate Reconstitute(Document document,
        IEnumerable<DocumentVersion> versions,
        IEnumerable<string> tags)
        => new(document, versions.ToList().AsReadOnly(), tags.ToList().AsReadOnly());

    /// <summary>Upload a new version. Enforces check-out rule.</summary>
    public DocumentVersion AddVersion(FileMetadata file, int createdBy, string? changeNote = null)
    {
        if (!Document.IsCheckedOut)
            throw new InvalidOperationException("Document must be checked out before adding a new version.");

        // Determine version number
        var latestMajor = Versions.Any() ? Versions.Max(v => v.MajorVersion) : 0;
        var latestMinor = Versions.Where(v => v.MajorVersion == latestMajor).Max(v => (int?)v.MinorVersion) ?? 0;
        var newMinor = latestMinor + 1;
        var versionLabel = $"{latestMajor}.{newMinor}";

        // Mark all current versions as superseded
        foreach (var v in Versions.Where(v => v.IsCurrent))
            v.MarkSuperseded();

        var newVersion = DocumentVersion.Create(
            Document.DocumentId, versionLabel, latestMajor, newMinor, file, createdBy, changeNote);

        Document.CheckIn(0, createdBy); // versionId set after persistence
        return newVersion;
    }

    /// <summary>Can the document be submitted to workflow?</summary>
    public bool CanSubmitToWorkflow()
        => !Document.IsCheckedOut
        && !Document.IsLegalHold
        && Document.Status == DocumentStatus.Draft || Document.Status == DocumentStatus.Rejected;

    /// <summary>Apply legal hold to document — cascades from workspace or direct.</summary>
    public void ApplyLegalHold(int appliedBy)
    {
        Document.ApplyLegalHold();
        Document.RaiseDomainEvent(new LegalHoldAppliedEvent(Document.DocumentId, appliedBy));
    }

    /// <summary>Check if retention has expired.</summary>
    public bool IsRetentionExpired()
        => Document.RetentionExpiresAt.HasValue
        && Document.RetentionExpiresAt.Value <= DateOnly.FromDateTime(DateTime.UtcNow);
}

/// <summary>
/// WorkspaceAggregate — the consistency boundary for xECM workspace operations.
/// Governs document binding, security policy, metadata sync, and lifecycle.
/// </summary>
public class WorkspaceAggregate : IAggregateRoot
{
    public Workspace Workspace { get; private set; }
    public IReadOnlyList<WorkspaceSecurityPolicy> SecurityPolicies { get; private set; }
    public IReadOnlyList<WorkspaceDocument> BoundDocuments { get; private set; }

    private WorkspaceAggregate(Workspace workspace,
        IReadOnlyList<WorkspaceSecurityPolicy> policies,
        IReadOnlyList<WorkspaceDocument> documents)
    {
        Workspace = workspace;
        SecurityPolicies = policies;
        BoundDocuments = documents;
    }

    public static WorkspaceAggregate Reconstitute(Workspace workspace,
        IEnumerable<WorkspaceSecurityPolicy> policies,
        IEnumerable<WorkspaceDocument> documents)
        => new(workspace, policies.ToList().AsReadOnly(), documents.ToList().AsReadOnly());

    /// <summary>Bind workspace to an external system object.</summary>
    public void LinkToExternal(string systemId, string objectId, string objectType,
        string? objectUrl, int linkedBy)
    {
        if (Workspace.IsBoundToExternal)
            throw new InvalidOperationException(
                $"Workspace is already bound to {Workspace.ExternalSystemId}/{Workspace.ExternalObjectId}.");

        Workspace.BindToExternal(systemId, objectId, objectType, objectUrl, linkedBy);
        Workspace.RaiseDomainEvent(new WorkspaceLinkedToExternalEvent(
            Workspace.WorkspaceId, systemId, objectId, objectType, linkedBy));
    }

    /// <summary>Archive workspace — cascades legal hold and archival to bound documents.</summary>
    public void Archive(int archivedBy)
    {
        Workspace.Archive(archivedBy);
        Workspace.RaiseDomainEvent(new WorkspaceArchivedEvent(Workspace.WorkspaceId, archivedBy));
    }

    /// <summary>Apply legal hold at workspace level.</summary>
    public void ApplyLegalHold(int appliedBy)
    {
        Workspace.ApplyLegalHold();
        Workspace.RaiseDomainEvent(new WorkspaceLegalHoldAppliedEvent(
            Workspace.WorkspaceId, appliedBy, BoundDocuments.Count));
    }

    /// <summary>Validate if a principal has a specific permission on this workspace.</summary>
    public bool HasPermission(int userId, IEnumerable<int> userRoleIds,
        int? userDeptId, Func<WorkspaceSecurityPolicy, bool> permissionSelector)
    {
        var activePolicies = SecurityPolicies
            .Where(p => p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow);

        // Explicit deny wins
        bool hasDeny = activePolicies.Any(p => p.IsDeny && permissionSelector(p) &&
            MatchesPrincipal(p, userId, userRoleIds, userDeptId));
        if (hasDeny) return false;

        return activePolicies.Any(p => !p.IsDeny && permissionSelector(p) &&
            MatchesPrincipal(p, userId, userRoleIds, userDeptId));
    }

    private static bool MatchesPrincipal(WorkspaceSecurityPolicy policy, int userId,
        IEnumerable<int> roleIds, int? deptId) =>
        (policy.PrincipalType == "User"       && policy.PrincipalId == userId)    ||
        (policy.PrincipalType == "Role"       && roleIds.Contains(policy.PrincipalId)) ||
        (policy.PrincipalType == "Department" && deptId.HasValue && policy.PrincipalId == deptId.Value);

    /// <summary>Business rule: total document count allowed per workspace type.</summary>
    public bool CanBindMoreDocuments(int maxDocuments = 10_000)
        => BoundDocuments.Count(d => d.IsActive) < maxDocuments;
}
