using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.Infrastructure.Persistence.Repositories;

// ─── BASE REPOSITORY ──────────────────────────────────────────────────────────
public abstract class BaseRepository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    protected readonly EcmDbContext Ctx;
    protected BaseRepository(EcmDbContext ctx) => Ctx = ctx;

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default)
        => await Ctx.Set<TEntity>().FindAsync(new[] { id }, ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
        => await Ctx.Set<TEntity>().AddAsync(entity, ct);

    public void Update(TEntity entity) => Ctx.Set<TEntity>().Update(entity);
    public void Remove(TEntity entity) => Ctx.Set<TEntity>().Remove(entity);
}

// ─── DOCUMENT REPOSITORY ──────────────────────────────────────────────────────
public sealed class DocumentRepository : BaseRepository<Document>, IDocumentRepository
{
    public DocumentRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<Document?> GetByGuidAsync(Guid id, CancellationToken ct = default)
        => await Ctx.Documents.FirstOrDefaultAsync(d => d.DocumentId == id, ct);

    public async Task<Document?> GetByNumberAsync(string number, CancellationToken ct = default)
        => await Ctx.Documents.FirstOrDefaultAsync(d => d.DocumentNumber == number, ct);

    public async Task<bool> NumberExistsAsync(string number, CancellationToken ct = default)
        => await Ctx.Documents.AnyAsync(d => d.DocumentNumber == number, ct);

    public async Task<int> CountByLibraryAsync(int libraryId, CancellationToken ct = default)
        => await Ctx.Documents.CountAsync(d => d.LibraryId == libraryId, ct);

    public async Task<IEnumerable<Document>> GetExpiringRetentionAsync(
        int daysAhead, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        return await Ctx.Documents
            .Where(d => d.RetentionExpiresAt.HasValue
                     && d.RetentionExpiresAt.Value <= cutoff
                     && !d.IsLegalHold)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Document>> GetCheckedOutByUserAsync(
        int userId, CancellationToken ct = default)
        => await Ctx.Documents
            .Where(d => d.CheckedOutBy == userId && d.IsCheckedOut)
            .ToListAsync(ct);
}

// ─── DOCUMENT VERSION REPOSITORY ─────────────────────────────────────────────
public sealed class DocumentVersionRepository : BaseRepository<DocumentVersion>, IDocumentVersionRepository
{
    public DocumentVersionRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<DocumentVersion?> GetCurrentAsync(Guid documentId, CancellationToken ct = default)
        => await Ctx.DocumentVersions
            .Where(v => v.DocumentId == documentId && v.IsCurrent)
            .OrderByDescending(v => v.MajorVersion)
            .ThenByDescending(v => v.MinorVersion)
            .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<DocumentVersion>> GetAllForDocumentAsync(
        Guid documentId, CancellationToken ct = default)
        => await Ctx.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.MajorVersion)
            .ThenByDescending(v => v.MinorVersion)
            .ToListAsync(ct);
}

// ─── USER REPOSITORY ──────────────────────────────────────────────────────────
public sealed class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await Ctx.Users.FirstOrDefaultAsync(
            u => u.Username == username.Trim().ToLowerInvariant(), ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await Ctx.Users.FirstOrDefaultAsync(
            u => u.Email == email.Trim().ToLowerInvariant(), ct);

    public async Task<User?> GetByExternalIdAsync(string externalId, CancellationToken ct = default)
        => await Ctx.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId, ct);

    public async Task<IEnumerable<string>> GetPermissionsAsync(int userId, CancellationToken ct = default)
        => await Ctx.Set<UserRole>()
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IEnumerable<int>> GetRoleIdsAsync(int userId, CancellationToken ct = default)
        => await Ctx.Set<UserRole>()
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

    public async Task<int?> GetDepartmentIdAsync(int userId, CancellationToken ct = default)
        => await Ctx.Users
            .Where(u => u.UserId == userId)
            .Select(u => u.DepartmentId)
            .FirstOrDefaultAsync(ct);
}

// ─── WORKFLOW REPOSITORY ──────────────────────────────────────────────────────
public sealed class WorkflowRepository : BaseRepository<WorkflowInstance>, IWorkflowRepository
{
    public WorkflowRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<WorkflowInstance?> GetActiveForDocumentAsync(
        Guid documentId, CancellationToken ct = default)
        => await Ctx.WorkflowInstances
            .FirstOrDefaultAsync(i => i.DocumentId == documentId
                                   && i.Status == "InProgress", ct);

    public async Task<IEnumerable<WorkflowTask>> GetUserInboxAsync(
        int userId, IEnumerable<int> roleIds, CancellationToken ct = default)
        => await Ctx.WorkflowTasks
            .Where(t => t.Status == "Pending"
                     && (t.AssignedToUserId == userId
                         || roleIds.Contains(t.AssignedToRoleId ?? 0)))
            .OrderBy(t => t.IsOverdue ? 0 : 1)
            .ThenBy(t => t.DueAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<WorkflowTask>> GetOverdueTasksAsync(
        CancellationToken ct = default)
        => await Ctx.WorkflowTasks
            .Where(t => t.Status == "Pending"
                     && t.DueAt.HasValue
                     && t.DueAt.Value < DateTime.UtcNow
                     && !t.IsOverdue)
            .ToListAsync(ct);

    public async Task<WorkflowTask?> GetTaskAsync(int taskId, CancellationToken ct = default)
        => await Ctx.WorkflowTasks
            .FirstOrDefaultAsync(t => t.TaskId == taskId, ct);
}

// Placeholder entity classes referenced by repositories (defined in SQL schema)
// In a full project these would be in separate files
public class UserRole  { public int UserId { get; set; } public int RoleId { get; set; } public bool IsActive { get; set; } public Role Role { get; set; } = null!; }
public class Role      { public int RoleId { get; set; } public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>(); }
public class RolePermission { public int RoleId { get; set; } public int PermissionId { get; set; } public Permission Permission { get; set; } = null!; }
public class Permission { public int PermissionId { get; set; } public string PermissionCode { get; set; } = string.Empty; }
