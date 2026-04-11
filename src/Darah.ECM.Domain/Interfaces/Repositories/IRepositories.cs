using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Entities;

namespace Darah.ECM.Domain.Interfaces.Repositories;

/// <summary>Generic repository — basic persistence operations for one entity type.</summary>
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}

/// <summary>Document-specific repository queries.</summary>
public interface IDocumentRepository : IRepository<Document>
{
    Task<Document?> GetByGuidAsync(Guid id, CancellationToken ct = default);
    Task<Document?> GetByNumberAsync(string number, CancellationToken ct = default);
    Task<bool>      NumberExistsAsync(string number, CancellationToken ct = default);
    Task<int>       CountByLibraryAsync(int libraryId, CancellationToken ct = default);
    Task<IEnumerable<Document>> GetExpiringRetentionAsync(int daysAhead, CancellationToken ct = default);
    Task<IEnumerable<Document>> GetCheckedOutByUserAsync(int userId, CancellationToken ct = default);
}

/// <summary>DocumentVersion-specific repository.</summary>
public interface IDocumentVersionRepository : IRepository<DocumentVersion>
{
    Task<DocumentVersion?> GetCurrentAsync(Guid documentId, CancellationToken ct = default);
    Task<IEnumerable<DocumentVersion>> GetAllForDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<int>  GetNextMinorVersionAsync(Guid documentId, int majorVersion, CancellationToken ct = default);
}

/// <summary>User repository.</summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByExternalIdAsync(string externalId, CancellationToken ct = default);
    Task<IEnumerable<string>> GetPermissionsAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<int>>   GetRoleIdsAsync(int userId, CancellationToken ct = default);
    Task<int?>               GetDepartmentIdAsync(int userId, CancellationToken ct = default);
}

/// <summary>Workflow repository.</summary>
public interface IWorkflowRepository : IRepository<WorkflowInstance>
{
    Task<WorkflowInstance?> GetActiveForDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<IEnumerable<WorkflowTask>> GetUserInboxAsync(int userId, IEnumerable<int> roleIds, CancellationToken ct = default);
    Task<IEnumerable<WorkflowTask>> GetOverdueTasksAsync(CancellationToken ct = default);
    Task<WorkflowTask?> GetTaskAsync(int taskId, CancellationToken ct = default);
}

/// <summary>
/// Unit of Work — guarantees atomic commits across all repositories.
///
/// DOMAIN EVENT DISPATCH:
///   After CommitAsync(), call DispatchDomainEventsAsync() to publish events that
///   were raised during the operation. Events are only dispatched AFTER successful commit,
///   ensuring they always reflect persisted state.
///   IUnitOfWork collects pending events from tracked entities before SaveChanges
///   and dispatches them via IEventBus after the transaction commits.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IDocumentRepository        Documents        { get; }
    IDocumentVersionRepository DocumentVersions { get; }
    IUserRepository            Users            { get; }
    IWorkflowRepository        Workflows        { get; }

    Task<int> CommitAsync(CancellationToken ct = default);
    Task      DispatchDomainEventsAsync(CancellationToken ct = default);

    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
