using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.xECM.Domain.Entities;

namespace Darah.ECM.xECM.Domain.Interfaces;

public interface IWorkspaceRepository : IRepository<Workspace>
{
    Task<Workspace?> GetByGuidAsync(Guid id, CancellationToken ct = default);
    Task<Workspace?> GetByExternalObjectAsync(string systemId, string objectId, CancellationToken ct = default);
    Task<bool> ExternalBindingExistsAsync(string systemId, string objectId, CancellationToken ct = default);
    Task<IEnumerable<Workspace>> GetPendingSyncAsync(string systemCode, int limit = 100, CancellationToken ct = default);
    Task<int> CountDocumentsAsync(Guid workspaceId, CancellationToken ct = default);
}

public interface IMetadataSyncEngine
{
    Task<SyncResult> TriggerSyncAsync(Guid workspaceId, SyncDirection direction, CancellationToken ct = default);
    Task<BulkSyncResult> BulkSyncAsync(string systemCode, CancellationToken ct = default);
    Task<bool> ResolveConflictAsync(Guid workspaceId, int fieldId, string resolution, CancellationToken ct = default);
}

public enum SyncDirection { Inbound, Outbound, Bidirectional }
public record SyncResult(bool IsSuccess, int FieldsUpdated, int ConflictsDetected, string? ErrorMessage = null, long DurationMs = 0);
public record BulkSyncResult(int WorkspacesSynced, int WorkspacesFailed, int TotalFieldsUpdated);
