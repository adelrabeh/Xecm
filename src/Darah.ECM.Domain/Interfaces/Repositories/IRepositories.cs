using Darah.ECM.Domain.Entities;

namespace Darah.ECM.Domain.Interfaces.Repositories;

/// <summary>Generic repository — only aggregate roots implement this directly.</summary>
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}

public interface IDocumentRepository : IRepository<Document>
{
    Task<Document?> GetByGuidAsync(Guid id, CancellationToken ct = default);
    Task<Document?> GetByNumberAsync(string number, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(string number, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetExpiringRetentionAsync(int daysAhead, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetCheckedOutByUserAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(Guid documentId, CancellationToken ct = default);
}

public interface IDocumentVersionRepository : IRepository<DocumentVersion>
{
    Task<DocumentVersion?> GetCurrentVersionAsync(Guid documentId, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByExternalIdAsync(string externalId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPermissionsAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetRoleIdsAsync(int userId, CancellationToken ct = default);
}

public interface IWorkflowRepository : IRepository<WorkflowInstance>
{
    Task<WorkflowInstance?> GetActiveForDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTask>> GetUserInboxAsync(int userId, IEnumerable<int> roleIds, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTask>> GetOverdueTasksAsync(CancellationToken ct = default);
    Task<WorkflowTask?> GetTaskAsync(int taskId, CancellationToken ct = default);
}

/// <summary>
/// Unit of Work — groups all repositories under a single transaction boundary.
/// Callers commit once; the infrastructure layer handles the database transaction.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IDocumentRepository        Documents        { get; }
    IDocumentVersionRepository DocumentVersions { get; }
    IUserRepository            Users            { get; }
    IWorkflowRepository        Workflows        { get; }

    Task<int>  CommitAsync(CancellationToken ct = default);
    Task       BeginTransactionAsync(CancellationToken ct = default);
    Task       CommitTransactionAsync(CancellationToken ct = default);
    Task       RollbackTransactionAsync(CancellationToken ct = default);
}
