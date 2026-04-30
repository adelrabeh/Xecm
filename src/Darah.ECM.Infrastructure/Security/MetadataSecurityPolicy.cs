using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.Infrastructure.Security;

/// <summary>AC2: Metadata-Driven Security</summary>
public sealed class MetadataSecurityPolicy
{
    private readonly EcmDbContext _db;
    public MetadataSecurityPolicy(EcmDbContext db) => _db = db;

    public async Task<DocumentAccessRight> EvaluateAsync(
        Guid documentId, int userId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (doc is null) return DocumentAccessRight.None;
        if (doc.CreatedBy == userId) return DocumentAccessRight.ReadWrite;

        var roleIds = await _db.Set<UserRoleAssignment>()
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.RoleId).ToListAsync(ct);

        var roles = await _db.Set<Role>()
            .Where(r => roleIds.Contains(r.RoleId))
            .Select(r => r.RoleCode).ToListAsync(ct);

        // ClassificationLevel uses Code property (not Value)
        return doc.Classification.Code switch
        {
            "TOP_SECRET"   => roles.Any(r => r is "Executive" or "SecurityAdmin")
                ? DocumentAccessRight.ReadOnly : DocumentAccessRight.None,
            "CONFIDENTIAL" => roles.Any(r => r is "Manager" or "Executive" or "SecurityAdmin")
                ? DocumentAccessRight.ReadWrite : DocumentAccessRight.None,
            "INTERNAL"     => DocumentAccessRight.ReadWrite,
            _              => DocumentAccessRight.ReadWrite
        };
    }

    public async Task<DocumentAccessRight> EvaluateByMetadataAsync(
        Guid documentId, int userId, CancellationToken ct = default)
    {
        // MetadataField uses FieldCode, DocumentMetadataValue uses TextValue
        var metadataValues = await _db.Set<DocumentMetadataValue>()
            .Where(m => m.DocumentId == documentId)
            .Join(_db.Set<MetadataField>(), m => m.FieldId, f => f.FieldId,
                (m, f) => new { f.FieldCode, m.TextValue })
            .ToListAsync(ct);

        var docClass = metadataValues
            .FirstOrDefault(m => m.FieldCode == "DocumentClass")?.TextValue;

        return docClass switch
        {
            "Financial Report"  => await IsInDepartmentAsync(userId, "Finance", ct)
                ? DocumentAccessRight.ReadWrite : DocumentAccessRight.None,
            "HR Confidential"   => await IsInDepartmentAsync(userId, "HumanResources", ct)
                ? DocumentAccessRight.ReadWrite : DocumentAccessRight.None,
            "Legal Document"    => await IsInDepartmentAsync(userId, "Legal", ct)
                ? DocumentAccessRight.ReadWrite : DocumentAccessRight.ReadOnly,
            _                   => await EvaluateAsync(documentId, userId, ct)
        };
    }

    private async Task<bool> IsInDepartmentAsync(int userId, string deptName, CancellationToken ct)
    {
        var user = await _db.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user?.DepartmentId is null) return false;
        var dept = await _db.Set<Department>().AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == user.DepartmentId, ct);
        return dept?.NameEn?.Contains(deptName, StringComparison.OrdinalIgnoreCase) == true;
    }
}

public enum DocumentAccessRight { None, ReadOnly, ReadWrite, FullControl }
