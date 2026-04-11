using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.Infrastructure.Persistence;

// ─── DB CONTEXT ───────────────────────────────────────────────────────────────
public sealed class EcmDbContext : DbContext
{
    private readonly ICurrentUserAccessor? _currentUser;

    public EcmDbContext(DbContextOptions<EcmDbContext> options,
        ICurrentUserAccessor? currentUser = null) : base(options)
        => _currentUser = currentUser;

    // Core ECM
    public DbSet<User>            Users            => Set<User>();
    public DbSet<Document>        Documents        => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<AuditLog>        AuditLogs        => Set<AuditLog>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask>    WorkflowTasks    => Set<WorkflowTask>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
        mb.ApplyConfigurationsFromAssembly(typeof(EcmDbContext).Assembly);

        // ── Global soft-delete query filters ────────────────────────────────
        mb.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);
        mb.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        // ── GUID defaults ────────────────────────────────────────────────────
        mb.Entity<Document>()
          .Property(d => d.DocumentId)
          .HasDefaultValueSql("NEWSEQUENTIALID()");

        // ── DocumentStatus value converter (string ↔ DocumentStatus VO) ─────
        mb.Entity<Document>()
          .Property(d => d.Status)
          .HasConversion(
              v => v.Value,
              v => DocumentStatus.From(v))
          .HasColumnType("NVARCHAR(20)");

        // ── ClassificationLevel value converter (int ↔ ClassificationLevel VO) ─
        mb.Entity<Document>()
          .Property(d => d.Classification)
          .HasConversion(
              v => v.Order,
              v => ClassificationLevel.FromOrder(v))
          .HasColumnName("ClassificationLevelId");

        // ── FileMetadata owned entity for DocumentVersion ─────────────────
        mb.Entity<DocumentVersion>()
          .OwnsOne(v => v.File, file =>
          {
              file.Property(f => f.StorageKey).HasColumnName("StorageKey").HasMaxLength(1000);
              file.Property(f => f.OriginalFileName).HasColumnName("OriginalFileName").HasMaxLength(500);
              file.Property(f => f.ContentType).HasColumnName("ContentType").HasMaxLength(200);
              file.Property(f => f.FileExtension).HasColumnName("FileExtension").HasMaxLength(20);
              file.Property(f => f.FileSizeBytes).HasColumnName("FileSizeBytes");
              file.Property(f => f.ContentHash).HasColumnName("ContentHash").HasMaxLength(64);
              file.Property(f => f.StorageProvider).HasColumnName("StorageProvider").HasMaxLength(50);
          });

        // ── AuditLog — append-only; no soft-delete filter ───────────────────
        mb.Entity<AuditLog>().ToTable("AuditLogs").HasKey(a => a.AuditId);
        mb.Entity<AuditLog>().Property(a => a.AuditId).ValueGeneratedOnAdd();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var userId = _currentUser?.UserId;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added && userId.HasValue)
                entry.Entity.SetCreated(userId.Value);
            else if (entry.State == EntityState.Modified && userId.HasValue)
                entry.Entity.SetUpdated(userId.Value);
        }
        return await base.SaveChangesAsync(ct);
    }
}

/// <summary>Thin accessor so DbContext does not depend on the full ICurrentUser contract.</summary>
public interface ICurrentUserAccessor { int? UserId { get; } }

// ─── UNIT OF WORK ─────────────────────────────────────────────────────────────
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly EcmDbContext _ctx;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _tx;

    public UnitOfWork(EcmDbContext ctx,
        IDocumentRepository        documents,
        IDocumentVersionRepository documentVersions,
        IUserRepository            users,
        IWorkflowRepository        workflows)
    {
        _ctx             = ctx;
        Documents        = documents;
        DocumentVersions = documentVersions;
        Users            = users;
        Workflows        = workflows;
    }

    public IDocumentRepository        Documents        { get; }
    public IDocumentVersionRepository DocumentVersions { get; }
    public IUserRepository            Users            { get; }
    public IWorkflowRepository        Workflows        { get; }

    public async Task<int> CommitAsync(CancellationToken ct = default)
        => await _ctx.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _tx = await _ctx.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is not null) await _tx.CommitAsync(ct);
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is not null) await _tx.RollbackAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_tx is not null) await _tx.DisposeAsync();
        await _ctx.DisposeAsync();
    }
}
