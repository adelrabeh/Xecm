using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.Infrastructure.Security;

/// <summary>
/// AC2: Metadata-Driven Security
/// Dynamically assigns access rights based on document metadata.
/// Rule: if DocumentClass = "Financial Report" → only Finance Team gets Read/Write
/// </summary>
public sealed class MetadataSecurityPolicy
{
    private readonly EcmDbContext _db;

    public MetadataSecurityPolicy(EcmDbContext db) => _db = db;

    public async Task<DocumentAccessRight> EvaluateAsync(
        Guid documentId, int userId, CancellationToken ct = default)
    {
        var doc = await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);

        if (doc is null) return DocumentAccessRight.None;

        // Get user roles
        var roleIds = await _db.Set<UserRole>()
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        var roles = await _db.Set<Role>()
            .Where(r => roleIds.Contains(r.RoleId))
            .Select(r => r.RoleCode)
            .ToListAsync(ct);

        // Owner always has full access
        if (doc.CreatedBy == userId) return DocumentAccessRight.ReadWrite;

        // Classification-based rules (ISO 15489 compliant)
        return doc.Classification.Value switch
        {
            "TopSecret" => roles.Any(r => r is "Executive" or "SecurityAdmin")
                ? DocumentAccessRight.ReadOnly
                : DocumentAccessRight.None,

            "Confidential" => roles.Any(r => r is "Manager" or "Executive" or "SecurityAdmin")
                ? DocumentAccessRight.ReadWrite
                : DocumentAccessRight.None,

            "Internal" => roles.Any(r => r is not "External")
                ? DocumentAccessRight.ReadWrite
                : DocumentAccessRight.None,

            _ => DocumentAccessRight.ReadWrite // Public
        };
    }

    /// <summary>
    /// Evaluate based on metadata field values (e.g., DocumentClass = "Financial Report")
    /// </summary>
    public async Task<DocumentAccessRight> EvaluateByMetadataAsync(
        Guid documentId, int userId, CancellationToken ct = default)
    {
        // Get document metadata values
        var metadataValues = await _db.Set<DocumentMetadataValue>()
            .Where(m => m.DocumentId == documentId)
            .Join(_db.Set<MetadataField>(), m => m.FieldId, f => f.FieldId,
                (m, f) => new { f.FieldName, m.Value })
            .ToListAsync(ct);

        var docClass = metadataValues
            .FirstOrDefault(m => m.FieldName == "DocumentClass")?.Value;

        var userDept = await _db.Set<User>()
            .Where(u => u.UserId == userId)
            .Select(u => u.DepartmentId)
            .FirstOrDefaultAsync(ct);

        // Metadata-driven rules
        return docClass switch
        {
            "Financial Report" => await IsInDepartmentAsync(userId, "Finance", ct)
                ? DocumentAccessRight.ReadWrite
                : DocumentAccessRight.None,

            "HR Confidential" => await IsInDepartmentAsync(userId, "HumanResources", ct)
                ? DocumentAccessRight.ReadWrite
                : DocumentAccessRight.None,

            "Legal Document" => await IsInDepartmentAsync(userId, "Legal", ct)
                ? DocumentAccessRight.ReadWrite
                : DocumentAccessRight.ReadOnly,

            _ => await EvaluateAsync(documentId, userId, ct)
        };
    }

    private async Task<bool> IsInDepartmentAsync(int userId, string deptName, CancellationToken ct)
    {
        var user = await _db.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user?.DepartmentId is null) return false;

        var dept = await _db.Set<Department>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == user.DepartmentId, ct);

        return dept?.NameEn?.Contains(deptName, StringComparison.OrdinalIgnoreCase) == true;
    }
}

public enum DocumentAccessRight { None, ReadOnly, ReadWrite, FullControl }
