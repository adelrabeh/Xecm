using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Interfaces.Repositories;

/// <summary>Generic repository — basic CRUD over a single aggregate/entity type.</summary>
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}

public interface IDocumentRepository : IRepository<Document>
{
    Task<Document?>    GetByGuidAsync(Guid documentId, CancellationToken ct = default);
    Task<Document?>    GetByNumberAsync(string number, CancellationToken ct = default);
    Task<bool>         NumberExistsAsync(string number, CancellationToken ct = default);
    Task<int>          CountByLibraryAsync(int libraryId, CancellationToken ct = default);
    Task<IEnumerable<Document>> GetExpiringRetentionAsync(int daysAhead, CancellationToken ct = default);
    Task<IEnumerable<Document>> GetCheckedOutByUserAsync(int userId, CancellationToken ct = default);
}

public interface IDocumentVersionRepository : IRepository<DocumentVersion>
{
    Task<DocumentVersion?> GetCurrentAsync(Guid documentId, CancellationToken ct = default);
    Task<IEnumerable<DocumentVersion>> GetAllForDocumentAsync(Guid documentId, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?>                GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?>                GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?>                GetByExternalIdAsync(string externalId, CancellationToken ct = default);
    Task<IEnumerable<string>>  GetPermissionsAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<int>>     GetRoleIdsAsync(int userId, CancellationToken ct = default);
    Task<int?>                 GetDepartmentIdAsync(int userId, CancellationToken ct = default);
}

public interface IWorkflowRepository : IRepository<WorkflowInstance>
{
    Task<WorkflowInstance?> GetActiveForDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<IEnumerable<WorkflowTask>> GetUserInboxAsync(int userId, IEnumerable<int> roleIds, CancellationToken ct = default);
    Task<IEnumerable<WorkflowTask>> GetOverdueTasksAsync(CancellationToken ct = default);
    Task<WorkflowTask?>             GetTaskAsync(int taskId, CancellationToken ct = default);
}

/// <summary>Unit of Work — ensures atomic commits across multiple repositories.</summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IDocumentRepository        Documents        { get; }
    IDocumentVersionRepository DocumentVersions { get; }
    IUserRepository            Users            { get; }
    IWorkflowRepository        Workflows        { get; }

    Task<int> CommitAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

namespace Darah.ECM.Domain.Interfaces.Services;

using Darah.ECM.Domain.ValueObjects;

/// <summary>
/// File storage abstraction — provider-agnostic.
/// Implementations: LocalFileStorageService, S3FileStorageService, AzureBlobStorageService.
/// </summary>
public interface IFileStorageService
{
    string ProviderName { get; }
    Task<string>  StoreAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream>  RetrieveAsync(string storageKey, CancellationToken ct = default);
    Task          DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<bool>    ExistsAsync(string storageKey, CancellationToken ct = default);
    Task<string?> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplatedAsync(string toEmail, string templateCode, Dictionary<string, string> variables, CancellationToken ct = default);
}

/// <summary>Audit service — append-only; never modifies existing records.</summary>
public interface IAuditService
{
    Task LogAsync(
        string  eventType,
        string? entityType     = null,
        string? entityId       = null,
        object? oldValues      = null,
        object? newValues      = null,
        string  severity       = "Info",
        bool    isSuccessful   = true,
        string? failureReason  = null,
        string? additionalInfo = null,
        CancellationToken ct   = default);
}

/// <summary>Event bus — dispatches domain events to registered handlers.</summary>
public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
}

/// <summary>Event handler contract — one handler per event type.</summary>
public interface IEventHandler<in T> where T : class
{
    Task HandleAsync(T @event, CancellationToken ct = default);
}

/// <summary>Document number generator — unique, format-enforced serial numbers.</summary>
public interface IDocumentNumberGenerator
{
    Task<string> GenerateAsync(int documentTypeId, CancellationToken ct = default);
}

/// <summary>Workspace number generator.</summary>
public interface IWorkspaceNumberGenerator
{
    Task<string> GenerateAsync(int workspaceTypeId, CancellationToken ct = default);
}

/// <summary>Current authenticated user context — populated from JWT claims.</summary>
public interface ICurrentUser
{
    int                  UserId        { get; }
    string               Username      { get; }
    string               Email         { get; }
    string               FullNameAr    { get; }
    string?              FullNameEn    { get; }
    string               Language      { get; }
    string?              IPAddress     { get; }
    string?              SessionId     { get; }
    bool                 IsAuthenticated { get; }
    IEnumerable<string>  Permissions   { get; }
    bool HasPermission(string permission);
}

/// <summary>External system connector — one implementation per vendor (SAP, Salesforce, etc.).</summary>
public interface IExternalSystemConnector
{
    string SystemCode { get; }
    Task<bool>   TestConnectionAsync(CancellationToken ct = default);
    Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default);
    Task<bool>   PushUpdateAsync(string objectType, string objectId, Dictionary<string, object> fields, CancellationToken ct = default);
    Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(string objectType, DateTime since, CancellationToken ct = default);
}

public sealed record ExternalObjectPayload(
    string ObjectId,
    string ObjectType,
    IReadOnlyDictionary<string, object?> Fields,
    DateTime FetchedAt);
