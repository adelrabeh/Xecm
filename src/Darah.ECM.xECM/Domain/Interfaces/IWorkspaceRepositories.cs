using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.xECM.Domain.Entities;

namespace Darah.ECM.xECM.Domain.Interfaces;

public interface IWorkspaceRepository : IRepository<Workspace>
{
    new Task<int> CommitAsync(CancellationToken ct = default);
    Task<Workspace?> GetByGuidAsync(Guid id, CancellationToken ct = default);
    Task<Workspace?> GetByNumberAsync(string number, CancellationToken ct = default);
    Task<Workspace?> GetByExternalObjectAsync(string systemCode, string objectId, CancellationToken ct = default);
    Task<bool>       ExternalBindingExistsAsync(string systemCode, string objectId, CancellationToken ct = default);
    Task<IEnumerable<Workspace>> GetPendingSyncAsync(string systemCode, int limit = 100, CancellationToken ct = default);
    Task<IEnumerable<Workspace>> GetByStatusAsync(string statusCode, CancellationToken ct = default);
    Task<int>        CountDocumentsAsync(Guid workspaceId, CancellationToken ct = default);
}

public interface IWorkspaceDocumentRepository
{
    Task<WorkspaceDocument?> GetBindingAsync(Guid workspaceId, Guid documentId, CancellationToken ct = default);
    Task<IEnumerable<WorkspaceDocument>> GetByWorkspaceAsync(Guid workspaceId, bool activeOnly = true, CancellationToken ct = default);
    Task<IEnumerable<WorkspaceDocument>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<bool>       BindingExistsAsync(Guid workspaceId, Guid documentId, CancellationToken ct = default);
    Task             AddAsync(WorkspaceDocument binding, CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

public interface IExternalSystemRepository : IRepository<ExternalSystem>
{
    Task<ExternalSystem?> GetByCodeAsync(string systemCode, CancellationToken ct = default);
    Task<IEnumerable<ExternalSystem>> GetActiveAsync(CancellationToken ct = default);
}

public interface ISyncMappingRepository
{
    Task<IEnumerable<MetadataSyncMapping>> GetMappingsAsync(int systemId, string objectType, CancellationToken ct = default);
    Task<IEnumerable<MetadataSyncMapping>> GetActiveMappingsAsync(int systemId, CancellationToken ct = default);
    Task AddAsync(MetadataSyncMapping mapping, CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

public interface ISyncEventLogRepository
{
    Task AddAsync(SyncEventLog log, CancellationToken ct = default);
    Task<IEnumerable<SyncEventLog>> GetByWorkspaceAsync(Guid workspaceId, int page, int pageSize, CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

public interface IWorkspaceSecurityRepository
{
    Task<IEnumerable<WorkspaceSecurityPolicy>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
    Task AddAsync(WorkspaceSecurityPolicy policy, CancellationToken ct = default);
    Task RemoveAsync(int policyId, CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

public interface IWorkspaceMetadataRepository
{
    Task<IEnumerable<WorkspaceMetadataValue>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
    Task<WorkspaceMetadataValue?> GetValueAsync(Guid workspaceId, int fieldId, CancellationToken ct = default);
    Task AddAsync(WorkspaceMetadataValue value, CancellationToken ct = default);
    void Update(WorkspaceMetadataValue value);
    Task<int> CommitAsync(CancellationToken ct = default);
}

public interface IWorkspaceAuditRepository
{
    Task AddAsync(WorkspaceAuditLog log, CancellationToken ct = default);
    Task<IEnumerable<WorkspaceAuditLog>> GetByWorkspaceAsync(Guid workspaceId, int page, int pageSize, CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

// Workspace number generator
public interface IWorkspaceNumberGenerator
{
    Task<string> GenerateAsync(int workspaceTypeId, string typeCode, CancellationToken ct = default);
}
