using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.xECM.Domain.Entities;
using Darah.ECM.xECM.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.xECM.Infrastructure.Sync;

public enum SyncDirection { Inbound, Outbound, Bidirectional }
public record SyncResult(bool IsSuccess, int FieldsUpdated, int ConflictsDetected, string? ErrorMessage = null, long DurationMs = 0);
public record BulkSyncResult(int WorkspacesSynced, int WorkspacesFailed, int TotalFieldsUpdated);

public interface IMetadataSyncEngine
{
    Task<SyncResult>     TriggerSyncAsync(Guid workspaceId, SyncDirection direction, string triggerType = "Manual", CancellationToken ct = default);
    Task<BulkSyncResult> BulkSyncAsync(string systemCode, SyncDirection direction, CancellationToken ct = default);
    Task<bool>           ResolveConflictAsync(Guid workspaceId, int fieldId, string resolution, CancellationToken ct = default);
}

/// <summary>
/// Full Metadata Sync Engine with all 4 conflict strategies.
/// Conflict strategies: ExternalWins | InternalWins | Newer | Manual
/// Transform pipeline: uppercase|lowercase|trim|titlecase|prefix:X|suffix:X|truncate:N
/// </summary>
public sealed class MetadataSyncEngine : IMetadataSyncEngine
{
    private readonly ISyncMappingRepository _mappingRepo;
    private readonly IWorkspaceMetadataRepository _metaRepo;
    private readonly ISyncEventLogRepository _syncLogRepo;
    private readonly IWorkspaceRepository _wsRepo;
    private readonly IEnumerable<IExternalSystemConnector> _connectors;
    private readonly IExternalSystemRepository _systemRepo;
    private readonly IAuditService _audit;
    private readonly ILogger<MetadataSyncEngine> _logger;

    public MetadataSyncEngine(ISyncMappingRepository mappingRepo, IWorkspaceMetadataRepository metaRepo,
        ISyncEventLogRepository syncLogRepo, IWorkspaceRepository wsRepo,
        IEnumerable<IExternalSystemConnector> connectors, IExternalSystemRepository systemRepo,
        IAuditService audit, ILogger<MetadataSyncEngine> logger)
    { _mappingRepo = mappingRepo; _metaRepo = metaRepo; _syncLogRepo = syncLogRepo; _wsRepo = wsRepo;
      _connectors = connectors; _systemRepo = systemRepo; _audit = audit; _logger = logger; }

    public async Task<SyncResult> TriggerSyncAsync(Guid workspaceId, SyncDirection direction,
        string triggerType = "Manual", CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var workspace = await _wsRepo.GetByGuidAsync(workspaceId, ct);
        if (workspace is null || !workspace.IsBoundToExternal)
            return new SyncResult(false, 0, 0, "Workspace not found or not bound");

        var externalSystem = await _systemRepo.GetByCodeAsync(workspace.ExternalSystemCode!, ct);
        if (externalSystem is null || !externalSystem.IsActive)
            return new SyncResult(false, 0, 0, "External system not configured or inactive");

        var connector = _connectors.FirstOrDefault(c =>
            c.SystemCode.Equals(externalSystem.SystemCode, StringComparison.OrdinalIgnoreCase));
        if (connector is null) return new SyncResult(false, 0, 0, $"No connector for '{externalSystem.SystemCode}'");

        workspace.BeginSync(0);
        await _wsRepo.CommitAsync(ct);

        int fieldsUpdated = 0, conflicts = 0;
        try
        {
            if (!await connector.TestConnectionAsync(ct))
            {
                await RecordFailure(workspace, "Connection failed", sw.ElapsedMilliseconds, externalSystem.SystemId, direction, triggerType, ct);
                return new SyncResult(false, 0, 0, "Connection failed");
            }

            if (direction is SyncDirection.Inbound or SyncDirection.Bidirectional)
            {
                var (u, c) = await SyncInboundAsync(workspace, connector, externalSystem.SystemId, ct);
                fieldsUpdated += u; conflicts += c;
            }
            if (direction is SyncDirection.Outbound or SyncDirection.Bidirectional)
                fieldsUpdated += await SyncOutboundAsync(workspace, connector, externalSystem.SystemId, ct);

            sw.Stop();
            workspace.RecordSyncSuccess(DateTime.UtcNow, fieldsUpdated, 0);
            await _wsRepo.CommitAsync(ct);
            await _syncLogRepo.AddAsync(SyncEventLog.CreateSuccess(workspaceId, externalSystem.SystemId,
                direction.ToString(), triggerType, fieldsUpdated, conflicts, sw.ElapsedMilliseconds,
                workspace.ExternalObjectId, workspace.ExternalObjectType), ct);
            await _syncLogRepo.CommitAsync(ct);

            _logger.LogInformation("Sync OK: WS={W} Sys={S} Fields={F} Conflicts={C} {D}ms",
                workspaceId, externalSystem.SystemCode, fieldsUpdated, conflicts, sw.ElapsedMilliseconds);
            return new SyncResult(true, fieldsUpdated, conflicts, DurationMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            await RecordFailure(workspace, ex.Message, sw.ElapsedMilliseconds, externalSystem.SystemId, direction, triggerType, ct);
            return new SyncResult(false, fieldsUpdated, conflicts, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    public async Task<BulkSyncResult> BulkSyncAsync(string systemCode, SyncDirection direction, CancellationToken ct = default)
    {
        var sys = await _systemRepo.GetByCodeAsync(systemCode, ct);
        if (sys is null) return new BulkSyncResult(0, 0, 0);
        var connector = _connectors.FirstOrDefault(c => c.SystemCode.Equals(systemCode, StringComparison.OrdinalIgnoreCase));
        if (connector is null || !await connector.TestConnectionAsync(ct)) return new BulkSyncResult(0, 0, 0);
        var pending = await _wsRepo.GetPendingSyncAsync(systemCode, 100, ct);
        int synced = 0, failed = 0, totalFields = 0;
        foreach (var ws in pending)
        {
            var r = await TriggerSyncAsync(ws.WorkspaceId, direction, "Scheduled", ct);
            if (r.IsSuccess) { synced++; totalFields += r.FieldsUpdated; } else failed++;
        }
        return new BulkSyncResult(synced, failed, totalFields);
    }

    public async Task<bool> ResolveConflictAsync(Guid workspaceId, int fieldId, string resolution, CancellationToken ct = default)
    {
        if (resolution is not ("UseExternal" or "UseInternal")) return false;
        var value = await _metaRepo.GetValueAsync(workspaceId, fieldId, ct);
        if (value is null) return false;
        value.SourceType = resolution == "UseExternal" ? "ExternalSync" : "ManualOverride";
        _metaRepo.Update(value);
        await _metaRepo.CommitAsync(ct);
        await _audit.LogAsync("ConflictResolved", "WorkspaceMetadata", workspaceId.ToString(),
            additionalInfo: $"FieldId={fieldId} Resolution={resolution}", ct: ct);
        return true;
    }

    private async Task<(int updated, int conflicts)> SyncInboundAsync(Workspace workspace,
        IExternalSystemConnector connector, int systemId, CancellationToken ct)
    {
        var payload = await connector.FetchObjectAsync(workspace.ExternalObjectType!, workspace.ExternalObjectId!, ct);
        if (payload is null) return (0, 0);
        var mappings = (await _mappingRepo.GetMappingsAsync(systemId, workspace.ExternalObjectType!, ct))
            .Where(m => m.IsActive && m.SyncDirection is "InboundOnly" or "Bidirectional").ToList();

        int updated = 0, conflicts = 0;
        foreach (var mapping in mappings)
        {
            if (!payload.Fields.TryGetValue(mapping.ExternalFieldName, out var extObj)) continue;
            var extRaw = ApplyTransform(extObj?.ToString(), mapping.TransformExpression);
            var existing = await _metaRepo.GetValueAsync(workspace.WorkspaceId, mapping.InternalFieldId, ct);
            var curInternal = existing?.GetDisplayValue();

            bool hasConflict = !string.IsNullOrEmpty(curInternal) && curInternal != extRaw && existing?.SourceType == "Manual";
            string? winningValue;

            if (!hasConflict || mapping.ConflictStrategy == "ExternalWins")
                winningValue = extRaw;
            else if (mapping.ConflictStrategy == "Manual" && hasConflict)
            {
                _logger.LogWarning("Sync conflict WS={W} Field={F}: Ext={E} Int={I}", workspace.WorkspaceId, mapping.InternalFieldId, extRaw, curInternal);
                conflicts++; workspace.RecordSyncConflict(1, 0); continue;
            }
            else
                winningValue = ResolveConflict(extRaw, curInternal, payload.FetchedAt, existing?.UpdatedAt, mapping.ConflictStrategy);

            if (existing is null)
            {
                var nv = new WorkspaceMetadataValue { WorkspaceId = workspace.WorkspaceId, FieldId = mapping.InternalFieldId, SourceType = "ExternalSync", ExternalSyncedAt = payload.FetchedAt };
                nv.SetValue(mapping.ExternalFieldType, winningValue);
                await _metaRepo.AddAsync(nv, ct);
            }
            else { existing.SetValue(mapping.ExternalFieldType, winningValue); existing.SourceType = "ExternalSync"; existing.ExternalSyncedAt = payload.FetchedAt; _metaRepo.Update(existing); }
            updated++;
        }
        await _metaRepo.CommitAsync(ct);
        return (updated, conflicts);
    }

    private async Task<int> SyncOutboundAsync(Workspace workspace, IExternalSystemConnector connector, int systemId, CancellationToken ct)
    {
        var mappings = (await _mappingRepo.GetMappingsAsync(systemId, workspace.ExternalObjectType!, ct))
            .Where(m => m.IsActive && m.SyncDirection is "OutboundOnly" or "Bidirectional").ToList();
        if (!mappings.Any()) return 0;
        var fields = new Dictionary<string, object>();
        foreach (var m in mappings)
        {
            var v = await _metaRepo.GetValueAsync(workspace.WorkspaceId, m.InternalFieldId, ct);
            if (v?.GetDisplayValue() is string val) fields[m.ExternalFieldName] = val;
        }
        if (!fields.Any()) return 0;
        return await connector.PushUpdateAsync(workspace.ExternalObjectType!, workspace.ExternalObjectId!, fields, ct) ? fields.Count : 0;
    }

    private static string? ApplyTransform(string? raw, string? expr)
    {
        if (string.IsNullOrWhiteSpace(expr) || raw is null) return raw;
        return expr.Trim().ToLower() switch
        {
            "uppercase" => raw.ToUpperInvariant(),
            "lowercase" => raw.ToLowerInvariant(),
            "trim"      => raw.Trim(),
            "titlecase" => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(raw.ToLower()),
            var e when e.StartsWith("prefix:") => e[7..] + raw,
            var e when e.StartsWith("suffix:") => raw + e[7..],
            var e when e.StartsWith("truncate:") && int.TryParse(e[9..], out var len) => raw.Length > len ? raw[..len] : raw,
            _ => raw
        };
    }

    private static string? ResolveConflict(string? ext, string? intl, DateTime extAt, DateTime? intAt, string strategy) =>
        strategy switch
        {
            "ExternalWins" => ext,
            "InternalWins" => intl ?? ext,
            "Newer" => (extAt >= (intAt ?? DateTime.MinValue)) ? ext : intl,
            "Manual" => intl,
            _ => ext
        };

    private async Task RecordFailure(Workspace ws, string error, long ms, int systemId,
        SyncDirection dir, string trigger, CancellationToken ct)
    {
        ws.RecordSyncFailure(error, 0); await _wsRepo.CommitAsync(ct);
        await _syncLogRepo.AddAsync(SyncEventLog.CreateFailure(ws.WorkspaceId, systemId, dir.ToString(), trigger, error, ms), ct);
        await _syncLogRepo.CommitAsync(ct);
    }
}
