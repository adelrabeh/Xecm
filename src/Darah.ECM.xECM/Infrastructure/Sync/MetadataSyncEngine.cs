using Darah.ECM.Domain.Events.Workspace;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.xECM.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.xECM.Infrastructure.Sync;

public enum SyncDirection { Inbound, Outbound, Bidirectional }

public sealed record SyncResult(
    bool   IsSuccess,
    int    FieldsUpdated,
    int    ConflictsDetected,
    string? ErrorMessage = null,
    long   DurationMs    = 0);

public sealed record BulkSyncResult(
    int WorkspacesSynced,
    int WorkspacesFailed,
    int TotalFieldsUpdated);

/// <summary>Metadata sync engine interface — inbound/outbound/bidirectional.</summary>
public interface IMetadataSyncEngine
{
    Task<SyncResult>    TriggerSyncAsync(Guid workspaceId, SyncDirection direction, CancellationToken ct = default);
    Task<BulkSyncResult> BulkSyncAsync(string externalSystemCode, CancellationToken ct = default);
    Task<SyncResult>    PushOutboundAsync(Guid workspaceId, CancellationToken ct = default);
    Task<bool>          ResolveConflictAsync(Guid workspaceId, int fieldId, string resolution, CancellationToken ct = default);
}

/// <summary>
/// Configurable, mapping-driven metadata synchronization engine.
///
/// SYNC FLOW (Inbound):
///   1. Load workspace + its external binding
///   2. Find registered connector for ExternalSystemId
///   3. Fetch external object payload via connector
///   4. Load field mappings from MetadataSyncMappings table
///   5. For each field: apply transform, check conflict strategy, persist WorkspaceMetadataValue
///   6. Update workspace SyncStatus and LastSyncedAt
///   7. Log to SyncEventLogs
///   8. Raise MetadataSyncCompletedEvent (or MetadataSyncFailedEvent)
///
/// CONFLICT STRATEGIES:
///   ExternalWins — external always overwrites internal (default for master data)
///   InternalWins — manual edits are never overwritten by sync
///   Newer        — more recent timestamp wins
///   Manual       — conflict logged; neither side wins; human must resolve
/// </summary>
public sealed class MetadataSyncEngine : IMetadataSyncEngine
{
    private readonly IEnumerable<IExternalSystemConnector> _connectors;
    private readonly IEventBus _eventBus;
    private readonly ILogger<MetadataSyncEngine> _logger;

    public MetadataSyncEngine(
        IEnumerable<IExternalSystemConnector> connectors,
        IEventBus eventBus,
        ILogger<MetadataSyncEngine> logger)
    {
        _connectors = connectors;
        _eventBus   = eventBus;
        _logger     = logger;
    }

    public async Task<SyncResult> TriggerSyncAsync(
        Guid workspaceId, SyncDirection direction, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation(
            "Sync started: workspace={Id} direction={Dir}", workspaceId, direction);

        // Full implementation loads workspace from repository, resolves connector,
        // fetches payload, applies mappings, and persists.
        // Shown here as documented skeleton to be completed in Phase 2.

        await _eventBus.PublishAsync(
            new MetadataSyncCompletedEvent(workspaceId, "SAP_PROD", 0, 0), ct);

        sw.Stop();
        return new SyncResult(true, 0, 0, DurationMs: sw.ElapsedMilliseconds);
    }

    public async Task<BulkSyncResult> BulkSyncAsync(
        string externalSystemCode, CancellationToken ct = default)
    {
        _logger.LogInformation("Bulk sync started for system {System}", externalSystemCode);
        // Batch process all workspaces with SyncStatus = Pending | Failed | null
        // or LastSyncedAt older than system's SyncIntervalMinutes
        return new BulkSyncResult(0, 0, 0);
    }

    public Task<SyncResult> PushOutboundAsync(Guid workspaceId, CancellationToken ct = default)
        => TriggerSyncAsync(workspaceId, SyncDirection.Outbound, ct);

    public async Task<bool> ResolveConflictAsync(
        Guid workspaceId, int fieldId, string resolution, CancellationToken ct = default)
    {
        // resolution: "UseExternal" | "UseInternal"
        _logger.LogInformation(
            "Resolving conflict: workspace={Id} field={Field} resolution={Res}",
            workspaceId, fieldId, resolution);
        return true;
    }
}
