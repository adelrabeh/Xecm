using Microsoft.EntityFrameworkCore;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Common;

namespace Darah.ECM.Infrastructure.Persistence;

// ─────────────────────────────────────────────────────────────
// DB CONTEXT
// ─────────────────────────────────────────────────────────────
public class EcmDbContext : DbContext
{
    private readonly ICurrentUserAccessor? _currentUser;

    public EcmDbContext(DbContextOptions<EcmDbContext> options,
        ICurrentUserAccessor? currentUser = null) : base(options)
        => _currentUser = currentUser;

    // ECM Core
    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();
    public DbSet<DocumentLibrary> DocumentLibraries => Set<DocumentLibrary>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<MetadataField> MetadataFields => Set<MetadataField>();
    public DbSet<DocumentMetadataValue> DocumentMetadataValues => Set<DocumentMetadataValue>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<DocumentTag> DocumentTags => Set<DocumentTag>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<WorkflowAction> WorkflowActions => Set<WorkflowAction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();

    // xECM
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceType> WorkspaceTypes => Set<WorkspaceType>();
    public DbSet<WorkspaceDocument> WorkspaceDocuments => Set<WorkspaceDocument>();
    public DbSet<WorkspaceSecurityPolicy> WorkspaceSecurityPolicies => Set<WorkspaceSecurityPolicy>();
    public DbSet<WorkspaceMetadataValue> WorkspaceMetadataValues => Set<WorkspaceMetadataValue>();
    public DbSet<ExternalSystem> ExternalSystems => Set<ExternalSystem>();
    public DbSet<MetadataSyncMapping> MetadataSyncMappings => Set<MetadataSyncMapping>();
    public DbSet<SyncEventLog> SyncEventLogs => Set<SyncEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EcmDbContext).Assembly);

        // Global soft-delete query filters
        modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<DocumentLibrary>().HasQueryFilter(l => !l.IsDeleted);
        modelBuilder.Entity<Folder>().HasQueryFilter(f => !f.IsDeleted);
        modelBuilder.Entity<DocumentType>().HasQueryFilter(dt => !dt.IsDeleted);
        modelBuilder.Entity<Workspace>().HasQueryFilter(w => !w.IsDeleted);

        // Document GUID default
        modelBuilder.Entity<Document>()
            .Property(d => d.DocumentId)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        modelBuilder.Entity<Workspace>()
            .Property(w => w.WorkspaceId)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // AuditLog — no update/delete via EF (enforced by DB permissions too)
        modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");
        modelBuilder.Entity<AuditLog>().HasKey(a => a.AuditId);
        modelBuilder.Entity<AuditLog>().Property(a => a.AuditId).ValueGeneratedOnAdd();

        // Document Status stored as string
        modelBuilder.Entity<Document>()
            .Property(d => d.Status)
            .HasConversion(
                v => v.Value,
                v => DocumentStatus.From(v));

        // Classification stored as int
        modelBuilder.Entity<Document>()
            .Property(d => d.Classification)
            .HasConversion(
                v => v.Order,
                v => ClassificationLevel.FromOrder(v));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var userId = _currentUser?.UserId;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added when userId.HasValue:
                    entry.Entity.SetCreated(userId.Value);
                    break;
                case EntityState.Modified when userId.HasValue:
                    entry.Entity.SetUpdated(userId.Value);
                    break;
            }
        }
        return await base.SaveChangesAsync(ct);
    }
}

/// <summary>Thin wrapper so DbContext doesn't depend on ICurrentUser directly.</summary>
public interface ICurrentUserAccessor { int? UserId { get; } }

// ─────────────────────────────────────────────────────────────
// UNIT OF WORK
// ─────────────────────────────────────────────────────────────
public class UnitOfWork : IUnitOfWork
{
    private readonly EcmDbContext _context;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;

    public UnitOfWork(EcmDbContext context,
        IDocumentRepository documents,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        IWorkflowRepository workflows)
    {
        _context = context;
        Documents = documents;
        Workspaces = workspaces;
        Users = users;
        Workflows = workflows;
    }

    public IDocumentRepository Documents { get; }
    public IWorkspaceRepository Workspaces { get; }
    public IUserRepository Users { get; }
    public IWorkflowRepository Workflows { get; }

    public async Task<int> CommitAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _context.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null) await _transaction.CommitAsync(ct);
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null) await _transaction.RollbackAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null) await _transaction.DisposeAsync();
        await _context.DisposeAsync();
    }
}

// ─────────────────────────────────────────────────────────────
// REPOSITORIES
// ─────────────────────────────────────────────────────────────
public abstract class BaseRepository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    protected readonly EcmDbContext Context;
    protected BaseRepository(EcmDbContext context) => Context = context;

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default)
        => await Context.Set<TEntity>().FindAsync(new[] { id }, ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
        => await Context.Set<TEntity>().AddAsync(entity, ct);

    public void Update(TEntity entity) => Context.Set<TEntity>().Update(entity);
    public void Remove(TEntity entity) => Context.Set<TEntity>().Remove(entity);
}

public class DocumentRepository : BaseRepository<Document>, IDocumentRepository
{
    public DocumentRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<Document?> GetByGuidAsync(Guid id, CancellationToken ct = default)
        => await Context.Documents.FirstOrDefaultAsync(d => d.DocumentId == id, ct);

    public async Task<Document?> GetByNumberAsync(string number, CancellationToken ct = default)
        => await Context.Documents.FirstOrDefaultAsync(d => d.DocumentNumber == number, ct);

    public async Task<DocumentAggregate?> GetAggregateAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await Context.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.Library)
            .Include(d => d.Folder)
            .FirstOrDefaultAsync(d => d.DocumentId == id, ct);
        if (doc is null) return null;

        var versions = await Context.DocumentVersions
            .Where(v => v.DocumentId == id)
            .OrderByDescending(v => v.MajorVersion).ThenByDescending(v => v.MinorVersion)
            .ToListAsync(ct);

        var tags = await Context.DocumentTags
            .Where(t => t.DocumentId == id)
            .Select(t => t.Tag.NameAr)
            .ToListAsync(ct);

        return DocumentAggregate.Reconstitute(doc, versions, tags);
    }

    public async Task<bool> NumberExistsAsync(string number, CancellationToken ct = default)
        => await Context.Documents.AnyAsync(d => d.DocumentNumber == number, ct);

    public async Task<int> CountByLibraryAsync(int libraryId, CancellationToken ct = default)
        => await Context.Documents.CountAsync(d => d.LibraryId == libraryId, ct);

    public async Task<IEnumerable<Document>> GetExpiringRetentionAsync(int daysAhead, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        return await Context.Documents
            .Where(d => d.RetentionExpiresAt.HasValue && d.RetentionExpiresAt.Value <= cutoff && !d.IsLegalHold)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Document>> GetCheckedOutByUserAsync(int userId, CancellationToken ct = default)
        => await Context.Documents.Where(d => d.CheckedOutBy == userId && d.IsCheckedOut).ToListAsync(ct);
}

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await Context.Users.FirstOrDefaultAsync(u => u.Username == username.ToLower(), ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await Context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower(), ct);

    public async Task<User?> GetByExternalIdAsync(string externalId, CancellationToken ct = default)
        => await Context.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId, ct);

    public async Task<IEnumerable<string>> GetPermissionsAsync(int userId, CancellationToken ct = default)
        => await Context.UserRoles
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IEnumerable<int>> GetRoleIdsAsync(int userId, CancellationToken ct = default)
        => await Context.UserRoles
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);
}

public class WorkflowRepository : BaseRepository<WorkflowInstance>, IWorkflowRepository
{
    public WorkflowRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<WorkflowInstance?> GetActiveForDocumentAsync(Guid documentId, CancellationToken ct = default)
        => await Context.WorkflowInstances
            .Include(i => i.Definition).ThenInclude(d => d.Steps)
            .FirstOrDefaultAsync(i => i.DocumentId == documentId && i.Status == "InProgress", ct);

    public async Task<IEnumerable<WorkflowTask>> GetUserInboxAsync(
        int userId, IEnumerable<int> roleIds, CancellationToken ct = default)
        => await Context.WorkflowTasks
            .Include(t => t.Instance).ThenInclude(i => i.Document)
            .Include(t => t.Instance).ThenInclude(i => i.Definition)
            .Include(t => t.Step)
            .Where(t => t.Status == "Pending" &&
                (t.AssignedToUserId == userId || roleIds.Contains(t.AssignedToRoleId ?? 0)))
            .OrderBy(t => t.IsOverdue ? 0 : 1).ThenBy(t => t.DueAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<WorkflowTask>> GetOverdueTasksAsync(CancellationToken ct = default)
        => await Context.WorkflowTasks
            .Include(t => t.Step)
            .Include(t => t.Instance).ThenInclude(i => i.Definition)
            .Where(t => t.Status == "Pending" && t.DueAt.HasValue
                && t.DueAt.Value < DateTime.UtcNow && !t.IsOverdue)
            .ToListAsync(ct);

    public async Task<WorkflowTask?> GetTaskAsync(int taskId, CancellationToken ct = default)
        => await Context.WorkflowTasks
            .Include(t => t.Step)
            .Include(t => t.Instance)
            .Include(t => t.Actions)
            .FirstOrDefaultAsync(t => t.TaskId == taskId, ct);
}
