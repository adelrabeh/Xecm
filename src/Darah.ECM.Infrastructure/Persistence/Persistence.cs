using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Darah.ECM.Infrastructure.Persistence;

// ─── DB CONTEXT ───────────────────────────────────────────────────────────────
public sealed class EcmDbContext : DbContext
{
    private readonly ICurrentUserAccessor? _currentUser;

    public EcmDbContext(DbContextOptions<EcmDbContext> options,
        ICurrentUserAccessor? currentUser = null) : base(options)
        => _currentUser = currentUser;

    // Core
    public DbSet<User>            Users            => Set<User>();
    public DbSet<Document>        Documents        => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask>    WorkflowTasks    => Set<WorkflowTask>();

    // Platform entities
    public DbSet<Partition>         Partitions       => Set<Partition>();
    public DbSet<DocumentTask>      DocumentTasks    => Set<DocumentTask>();
    public DbSet<TaskComment>       TaskComments     => Set<TaskComment>();
    public DbSet<ApplicationRole>   ApplicationRoles => Set<ApplicationRole>();
    public DbSet<UserApplicationRole> UserAppRoles   => Set<UserApplicationRole>();
    public DbSet<OAuthClient>       OAuthClients     => Set<OAuthClient>();
    public DbSet<SystemConfig>      SystemConfigs    => Set<SystemConfig>();
    public DbSet<RecycleBinEntry>   RecycleBin       => Set<RecycleBinEntry>();
    public DbSet<UserGroup>         UserGroups       => Set<UserGroup>();
    public DbSet<GroupMember>       GroupMembers     => Set<GroupMember>();
    public DbSet<KnowledgeAsset>    KnowledgeAssets    => Set<KnowledgeAsset>();
    // Record Model
    public DbSet<RecordDomain>      RecordDomains      => Set<RecordDomain>();
    public DbSet<RecordType>        RecordTypes        => Set<RecordType>();
    public DbSet<MetadataFieldDef>  MetadataFieldDefs  => Set<MetadataFieldDef>();
    public DbSet<Record>            Records            => Set<Record>();
    public DbSet<RecordAttachment>  RecordAttachments  => Set<RecordAttachment>();
    // Escalation
    public DbSet<TaskEscalation>         TaskEscalations        => Set<TaskEscalation>();
    public DbSet<RetentionScheduleEntry> RetentionSchedules     => Set<RetentionScheduleEntry>();
    public DbSet<TaxonomyCategory>  TaxonomyCategories => Set<TaxonomyCategory>();
    public DbSet<DocumentCategory>  DocumentCategories => Set<DocumentCategory>();
    public DbSet<AuditLog>        AuditLogs        => Set<AuditLog>();


    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Apply all IEntityTypeConfiguration<T> from this assembly
        mb.ApplyConfigurationsFromAssembly(typeof(EcmDbContext).Assembly);

        // Global soft-delete filters
        mb.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);
        mb.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        // GUID defaults (NEWSEQUENTIALID() for insert-order performance)
        mb.Entity<Document>()
            .Property(d => d.DocumentId)
            .HasDefaultValueSql("gen_random_uuid()");


        // DocumentStatus stored as string (readable audit logs)
        mb.Entity<Document>()
            .Property(d => d.Status)
            .HasConversion(v => v.Value, v => DocumentStatus.From(v))
            .HasMaxLength(20);

        // ClassificationLevel stored as int (compact FK reference)
        mb.Entity<Document>()
            .Property(d => d.Classification)
            .HasConversion(v => v.Order, v => ClassificationLevel.FromOrder(v))
            .HasColumnName("ClassificationLevelOrder");

        // Primary key mappings (non-standard names — EF convention requires 'Id' or '[Type]Id')
        // Platform entities
        mb.Entity<Partition>().HasKey(e => e.PartitionId);
        mb.Entity<DocumentTask>().HasKey(e => e.TaskId);
        mb.Entity<TaskComment>().HasKey(e => e.CommentId);
        mb.Entity<ApplicationRole>().HasKey(e => e.AppRoleId);
        mb.Entity<UserApplicationRole>().HasKey(e => e.Id);
        mb.Entity<OAuthClient>().HasKey(e => e.ClientId);
        mb.Entity<SystemConfig>().HasKey(e => e.ConfigId);
        mb.Entity<RecycleBinEntry>().HasKey(e => e.EntryId);
        mb.Entity<UserGroup>().HasKey(e => e.GroupId);
        mb.Entity<GroupMember>().HasKey(e => e.MemberId);
        mb.Entity<KnowledgeAsset>().HasKey(e => e.AssetId);
        mb.Entity<RecordDomain>().HasKey(e => e.DomainId);
        mb.Entity<RecordType>().HasKey(e => e.TypeId);
        mb.Entity<MetadataFieldDef>().HasKey(e => e.FieldDefId);
        mb.Entity<MetadataFieldDef>().Property(e => e.DataType).HasConversion<int>();
        mb.Entity<MetadataFieldDef>().Property(e => e.Scope).HasConversion<int>();
        mb.Entity<Record>().HasKey(e => e.RecordId);
        mb.Entity<Record>().Property(e => e.Status).HasConversion<int>();
        mb.Entity<Record>().Property(e => e.SecurityLevel).HasConversion<int>();
        mb.Entity<RecordAttachment>().HasKey(e => e.AttachmentId);
        mb.Entity<TaskEscalation>().HasKey(e => e.EscalationId);
        mb.Entity<RetentionScheduleEntry>().HasKey(e => e.EntryId);
        mb.Entity<RetentionScheduleEntry>().Property(e => e.DisposalAction).HasConversion<int>();
        mb.Entity<RetentionScheduleEntry>().Property(e => e.Status).HasConversion<int>();
        mb.Entity<TaskEscalation>().Property(e => e.Status).HasConversion<int>();
        mb.Entity<TaskEscalation>().Property(e => e.EscalationLevel).HasConversion<int>();
        mb.Entity<TaskEscalation>().Property(e => e.EscalatedToRole).HasConversion<int>();
        mb.Entity<KnowledgeAsset>().Property(e => e.Type).HasConversion<int>();
        mb.Entity<KnowledgeAsset>().Property(e => e.Status).HasConversion<int>();
        mb.Entity<KnowledgeAsset>().Property(e => e.DigitizationStatus).HasConversion<int>();
        mb.Entity<KnowledgeAsset>().Property(e => e.OcrQuality).HasConversion<int>();
        mb.Entity<KnowledgeAsset>().Property(e => e.Confidentiality).HasConversion<int>();
        mb.Entity<KnowledgeAsset>().Property(e => e.Retention).HasConversion<int>();
        mb.Entity<TaxonomyCategory>().HasKey(e => e.CategoryId);
        mb.Entity<DocumentCategory>().HasKey(e => e.Id);
        // Core entities
        mb.Entity<User>().HasKey(u => u.UserId);
        mb.Entity<Document>().HasKey(d => d.DocumentId);
        mb.Entity<DocumentVersion>().HasKey(v => v.VersionId);
        mb.Entity<WorkflowInstance>().HasKey(i => i.InstanceId);
        mb.Entity<WorkflowTask>().HasKey(t => t.TaskId);
        mb.Entity<AuditLog>().HasKey(a => a.AuditId);
        // Supporting entities
        mb.Entity<UserRole>().HasKey(ur => ur.Id);
        mb.Entity<Role>().HasKey(r => r.RoleId);
        mb.Entity<Department>().HasKey(d => d.DepartmentId);
        // Folder & Library
        mb.Entity<Folder>().HasKey(e => e.FolderId);
        mb.Entity<DocumentLibrary>().HasKey(e => e.LibraryId);
        mb.Entity<DocumentRelation>().HasKey(e => e.RelationId);
        mb.Entity<CheckoutLock>().HasKey(e => e.LockId);
        // Metadata
        mb.Entity<MetadataField>().HasKey(e => e.FieldId);
        mb.Entity<DocumentTypeMetadataField>().HasKey(e => e.DocumentTypeId);
        mb.Entity<DocumentMetadataValue>().HasKey(e => e.ValueId);
        // Records & Legal
        mb.Entity<RecordClass>().HasKey(e => e.ClassId);
        mb.Entity<RetentionPolicy>().HasKey(e => e.PolicyId);
        mb.Entity<LegalHold>().HasKey(e => e.HoldId);
        mb.Entity<DocumentLegalHold>().HasKey(e => e.DocumentId);
        mb.Entity<DisposalRequest>().HasKey(e => e.RequestId);
        mb.Entity<Notification>().HasKey(e => e.NotificationId);
        // Workflow
        mb.Entity<WorkflowDefinition>().HasKey(e => e.DefinitionId);
        mb.Entity<WorkflowStep>().HasKey(e => e.StepId);
        mb.Entity<WorkflowCondition>().HasKey(e => e.ConditionId);
        mb.Entity<WorkflowAction>().HasKey(e => e.ActionId);
        mb.Entity<WorkflowDelegation>().HasKey(e => e.DelegationId);

        // FileMetadata owned entity on DocumentVersion
        mb.Entity<DocumentVersion>().OwnsOne(v => v.File, fm =>
        {
            fm.Property(f => f.StorageKey).HasMaxLength(1000).IsRequired();
            fm.Property(f => f.OriginalFileName).HasMaxLength(500).IsRequired();
            fm.Property(f => f.ContentType).HasMaxLength(200).IsRequired();
            fm.Property(f => f.FileExtension).HasMaxLength(20).IsRequired();
            fm.Property(f => f.FileSizeBytes).IsRequired();
            fm.Property(f => f.ContentHash).HasMaxLength(64).IsRequired();
            fm.Property(f => f.StorageProvider).HasMaxLength(50).IsRequired();
        });

        // AuditLog — append-only, no update/delete via EF tracking
        mb.Entity<AuditLog>().ToTable("AuditLogs");
        mb.Entity<AuditLog>().Property(a => a.AuditId).ValueGeneratedOnAdd();
        mb.Entity<AuditLog>().HasIndex(a => a.CreatedAt);
        mb.Entity<AuditLog>().HasIndex(a => new { a.EntityType, a.EntityId });
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

/// <summary>Minimal accessor so EcmDbContext doesn't take a hard ICurrentUser dependency.</summary>
public interface ICurrentUserAccessor
{
    int? UserId { get; }
}

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

    public async Task<IEnumerable<Document>> GetExpiringRetentionAsync(int daysAhead, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        return await Ctx.Documents
            .Where(d => d.RetentionExpiresAt.HasValue
                     && d.RetentionExpiresAt.Value <= cutoff
                     && !d.IsLegalHold)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Document>> GetCheckedOutByUserAsync(int userId, CancellationToken ct = default)
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
            .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<DocumentVersion>> GetAllForDocumentAsync(Guid documentId, CancellationToken ct = default)
        => await Ctx.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.MajorVersion)
            .ThenByDescending(v => v.MinorVersion)
            .ToListAsync(ct);

    public async Task<int> GetNextMinorVersionAsync(Guid documentId, int majorVersion, CancellationToken ct = default)
    {
        var maxMinor = await Ctx.DocumentVersions
            .Where(v => v.DocumentId == documentId && v.MajorVersion == majorVersion)
            .MaxAsync(v => (int?)v.MinorVersion, ct) ?? -1;
        return maxMinor + 1;
    }
}

// ─── USER REPOSITORY ─────────────────────────────────────────────────────────
public sealed class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await Ctx.Users.FirstOrDefaultAsync(u => u.Username == username.ToLowerInvariant(), ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await Ctx.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<User?> GetByExternalIdAsync(string externalId, CancellationToken ct = default)
        => await Ctx.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId, ct);

    public async Task<IEnumerable<string>> GetPermissionsAsync(int userId, CancellationToken ct = default)
    {
        // Get role IDs for this user
        var roleIds = await Ctx.Set<UserRole>()
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);
        // Return admin permission for admin roles (simplified for now)
        // Full implementation requires RolePermissions table join
        return roleIds.Count > 0 ? new[] { "documents.read", "documents.create", "workflow.submit" } : Array.Empty<string>();
    }

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

// ─── WORKFLOW REPOSITORY ─────────────────────────────────────────────────────
public sealed class WorkflowRepository : BaseRepository<WorkflowInstance>, IWorkflowRepository
{
    public WorkflowRepository(EcmDbContext ctx) : base(ctx) { }

    public async Task<WorkflowInstance?> GetActiveForDocumentAsync(Guid documentId, CancellationToken ct = default)
        => await Ctx.WorkflowInstances
            .FirstOrDefaultAsync(i => i.DocumentId == documentId && i.Status == "InProgress", ct);

    public async Task<IEnumerable<WorkflowTask>> GetUserInboxAsync(
        int userId, IEnumerable<int> roleIds, CancellationToken ct = default)
        => await Ctx.WorkflowTasks
            .Where(t => t.Status == "Pending"
                     && (t.AssignedToUserId == userId
                         || roleIds.Contains(t.AssignedToRoleId ?? 0)))
            .OrderBy(t => t.IsOverdue ? 0 : 1)
            .ThenBy(t => t.DueAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<WorkflowTask>> GetOverdueTasksAsync(CancellationToken ct = default)
        => await Ctx.WorkflowTasks
            .Where(t => t.Status == "Pending"
                     && t.DueAt.HasValue
                     && t.DueAt.Value < DateTime.UtcNow
                     && !t.IsOverdue)
            .ToListAsync(ct);

    public async Task<WorkflowTask?> GetTaskAsync(int taskId, CancellationToken ct = default)
        => await Ctx.WorkflowTasks.FindAsync(new object[] { taskId }, ct);
}
