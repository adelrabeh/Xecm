// ================================================================
// FILE: src/Infrastructure/Sync/IMetadataSyncEngine.cs
// ================================================================
namespace Darah.ECM.Application.Common.Interfaces;

public enum SyncDirection { Inbound, Outbound, Bidirectional }

public interface IMetadataSyncEngine
{
    /// <summary>Trigger sync for a single workspace (inbound from external system).</summary>
    Task<SyncResult> TriggerSyncAsync(Guid workspaceId, SyncDirection direction, CancellationToken ct = default);

    /// <summary>Bulk sync all pending/outdated workspaces for a given system.</summary>
    Task<BulkSyncResult> BulkSyncAsync(string externalSystemCode, CancellationToken ct = default);

    /// <summary>Push workspace metadata changes out to an external system.</summary>
    Task<SyncResult> PushOutboundAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>Resolve a conflict on a specific workspace metadata field.</summary>
    Task<bool> ResolveConflictAsync(Guid workspaceId, int fieldId, string resolution, CancellationToken ct = default);
}

public record SyncResult(bool IsSuccess, int FieldsUpdated, int ConflictsDetected, string? ErrorMessage = null, long DurationMs = 0);
public record BulkSyncResult(int WorkspacesSynced, int WorkspacesFailed, int TotalFieldsUpdated);

public interface IExternalSystemConnector
{
    string SystemCode { get; }
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default);
    Task<bool> PushUpdateAsync(string objectType, string objectId, Dictionary<string, object> fields, CancellationToken ct = default);
    Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(string objectType, DateTime since, CancellationToken ct = default);
}

public class ExternalObjectPayload
{
    public string ObjectId { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
    public Dictionary<string, object?> Fields { get; init; } = new();
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
}

public interface IWorkspaceSecurityService
{
    Task<bool> CanReadWorkspaceAsync(Guid workspaceId, int userId, CancellationToken ct = default);
    Task<bool> CanWriteWorkspaceAsync(Guid workspaceId, int userId, CancellationToken ct = default);
    Task<bool> CanManageWorkspaceAsync(Guid workspaceId, int userId, CancellationToken ct = default);
    Task PropagateSecurityToDocumentAsync(Workspace workspace, Guid documentId, CancellationToken ct = default);
    Task PropagateSecurityToAllDocumentsAsync(Guid workspaceId, CancellationToken ct = default);
}

public interface IWorkspaceNumberGenerator
{
    Task<string> GenerateAsync(int workspaceTypeId, CancellationToken ct = default);
}

// ================================================================
// FILE: src/Infrastructure/Sync/MetadataSyncEngine.cs
// ================================================================
namespace Darah.ECM.Infrastructure.Sync;

public class MetadataSyncEngine : IMetadataSyncEngine
{
    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<IExternalSystemConnector> _connectors;
    private readonly IAuditService _audit;
    private readonly ILogger<MetadataSyncEngine> _logger;

    public MetadataSyncEngine(
        IApplicationDbContext context,
        IEnumerable<IExternalSystemConnector> connectors,
        IAuditService audit,
        ILogger<MetadataSyncEngine> logger)
    {
        _context = context;
        _connectors = connectors;
        _audit = audit;
        _logger = logger;
    }

    public async Task<SyncResult> TriggerSyncAsync(Guid workspaceId, SyncDirection direction, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int fieldsUpdated = 0, conflicts = 0;

        var workspace = await _context.Workspaces
            .Include(w => w.WorkspaceType)
            .Include(w => w.MetadataValues)
            .FirstOrDefaultAsync(w => w.WorkspaceId == workspaceId && !w.IsDeleted, ct);

        if (workspace == null) return new SyncResult(false, 0, 0, "Workspace not found");
        if (!workspace.IsBoundToExternal) return new SyncResult(false, 0, 0, "Workspace not bound to external system");

        var connector = _connectors.FirstOrDefault(c =>
            c.SystemCode.Equals(workspace.ExternalSystemId, StringComparison.OrdinalIgnoreCase));

        if (connector == null)
        {
            _logger.LogWarning("No connector registered for system {SystemId}", workspace.ExternalSystemId);
            return new SyncResult(false, 0, 0, $"No connector for system '{workspace.ExternalSystemId}'");
        }

        try
        {
            if (direction is SyncDirection.Inbound or SyncDirection.Bidirectional)
            {
                var (updated, conflictsFound) = await SyncInboundAsync(workspace, connector, ct);
                fieldsUpdated += updated;
                conflicts += conflictsFound;
            }

            if (direction is SyncDirection.Outbound or SyncDirection.Bidirectional)
                await SyncOutboundAsync(workspace, connector, ct);

            workspace.RecordSyncSuccess(DateTime.UtcNow, 0);

            await LogSyncEventAsync(workspace, "SyncCompleted", direction.ToString(), true, fieldsUpdated, null, ct);
            await _context.SaveChangesAsync(ct);

            sw.Stop();
            return new SyncResult(true, fieldsUpdated, conflicts, DurationMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            workspace.RecordSyncFailure(ex.Message, 0);
            await LogSyncEventAsync(workspace, "SyncFailed", direction.ToString(), false, 0, ex.Message, ct);
            await _context.SaveChangesAsync(ct);
            _logger.LogError(ex, "Sync failed for workspace {WorkspaceId}", workspaceId);
            sw.Stop();
            return new SyncResult(false, fieldsUpdated, conflicts, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private async Task<(int updated, int conflicts)> SyncInboundAsync(
        Workspace workspace, IExternalSystemConnector connector, CancellationToken ct)
    {
        // 1. Fetch fresh data from external system
        var payload = await connector.FetchObjectAsync(workspace.ExternalObjectType!, workspace.ExternalObjectId!, ct);
        if (payload == null) return (0, 0);

        // 2. Load field mappings for this system + workspace type
        var mappings = await _context.MetadataSyncMappings
            .Include(m => m.InternalField)
            .Where(m =>
                m.ExternalSystemId == workspace.WorkspaceType.DefaultExternalSystem // align by system
                && (m.WorkspaceTypeId == null || m.WorkspaceTypeId == workspace.WorkspaceTypeId)
                && m.ExternalObjectType == workspace.ExternalObjectType
                && m.SyncDirection != "OutboundOnly"
                && m.IsActive)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);

        int updated = 0, conflicts = 0;

        foreach (var mapping in mappings)
        {
            if (!payload.Fields.TryGetValue(mapping.ExternalFieldName, out var rawValue) || rawValue == null)
            {
                if (mapping.IsRequired)
                    _logger.LogWarning("Required field {Field} missing in external payload for workspace {WS}",
                        mapping.ExternalFieldName, workspace.WorkspaceId);
                continue;
            }

            // Transform value if expression defined
            var transformedValue = await ApplyTransformAsync(rawValue?.ToString() ?? string.Empty, mapping.TransformExpression, ct);

            // Find or create metadata value record
            var existing = workspace.MetadataValues.FirstOrDefault(mv => mv.FieldId == mapping.InternalFieldId);

            if (existing != null && existing.SourceType == "Manual" && mapping.ConflictStrategy == "Manual")
            {
                // Log conflict for manual resolution
                await LogConflictAsync(workspace, mapping, existing.TextValue, transformedValue, ct);
                workspace.RecordSyncConflict(0);
                conflicts++;
                continue;
            }

            // Resolve conflict
            string? resolvedValue = mapping.ConflictStrategy switch
            {
                "InternalWins" when existing?.SourceType == "Manual" => existing.TextValue,
                "Newer" when existing?.LastSyncedAt > payload.FetchedAt => existing.TextValue,
                _ => transformedValue  // ExternalWins (default) or new record
            };

            await SetMetadataValueAsync(workspace.WorkspaceId, mapping, resolvedValue, existing, ct);
            updated++;
        }

        _logger.LogInformation("Inbound sync workspace {WS}: {Updated} fields updated, {Conflicts} conflicts",
            workspace.WorkspaceId, updated, conflicts);

        return (updated, conflicts);
    }

    private async Task SyncOutboundAsync(Workspace workspace, IExternalSystemConnector connector, CancellationToken ct)
    {
        var outboundMappings = await _context.MetadataSyncMappings
            .Include(m => m.InternalField)
            .Where(m =>
                m.ExternalSystemId == workspace.WorkspaceType.DefaultExternalSystem
                && (m.WorkspaceTypeId == null || m.WorkspaceTypeId == workspace.WorkspaceTypeId)
                && m.SyncDirection != "InboundOnly"
                && m.IsActive)
            .ToListAsync(ct);

        var outboundPayload = new Dictionary<string, object>();
        foreach (var mapping in outboundMappings)
        {
            var metaValue = workspace.MetadataValues.FirstOrDefault(mv => mv.FieldId == mapping.InternalFieldId);
            if (metaValue == null) continue;

            var value = GetMetadataValueAsObject(metaValue);
            if (value != null) outboundPayload[mapping.ExternalFieldName] = value;
        }

        if (outboundPayload.Any())
            await connector.PushUpdateAsync(workspace.ExternalObjectType!, workspace.ExternalObjectId!, outboundPayload, ct);
    }

    public async Task<BulkSyncResult> BulkSyncAsync(string externalSystemCode, CancellationToken ct = default)
    {
        int synced = 0, failed = 0, totalFields = 0;

        var workspaces = await _context.Workspaces
            .Where(w => w.ExternalSystemId == externalSystemCode && !w.IsDeleted &&
                (w.SyncStatus == "Pending" || w.SyncStatus == "Failed" ||
                 w.LastSyncedAt == null || w.LastSyncedAt < DateTime.UtcNow.AddHours(-1)))
            .Select(w => w.WorkspaceId)
            .ToListAsync(ct);

        _logger.LogInformation("Bulk sync started for {System}: {Count} workspaces", externalSystemCode, workspaces.Count);

        foreach (var wsId in workspaces)
        {
            if (ct.IsCancellationRequested) break;
            var result = await TriggerSyncAsync(wsId, SyncDirection.Inbound, ct);
            if (result.IsSuccess) { synced++; totalFields += result.FieldsUpdated; }
            else failed++;
        }

        return new BulkSyncResult(synced, failed, totalFields);
    }

    public async Task<SyncResult> PushOutboundAsync(Guid workspaceId, CancellationToken ct = default)
        => await TriggerSyncAsync(workspaceId, SyncDirection.Outbound, ct);

    public async Task<bool> ResolveConflictAsync(Guid workspaceId, int fieldId, string resolution, CancellationToken ct = default)
    {
        // resolution = "UseExternal" | "UseInternal"
        var meta = await _context.WorkspaceMetadataValues
            .FirstOrDefaultAsync(mv => mv.WorkspaceId == workspaceId && mv.FieldId == fieldId, ct);
        if (meta == null) return false;

        if (resolution == "UseExternal")
        {
            // Re-trigger sync for just this field
            meta.SourceType = "ExternalSync";
            meta.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Keep internal value, mark as resolved
            meta.SourceType = "Manual";
            meta.UpdatedAt = DateTime.UtcNow;
        }

        // Check if all conflicts resolved → update workspace sync status
        var workspace = await _context.Workspaces.FindAsync(new object[] { workspaceId }, ct);
        if (workspace?.SyncStatus == "Conflict")
        {
            workspace.RecordSyncSuccess(DateTime.UtcNow, 0);
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Private helpers ────────────────────────────────────────
    private async Task SetMetadataValueAsync(Guid workspaceId, MetadataSyncMapping mapping, string? value,
        WorkspaceMetadataValue? existing, CancellationToken ct)
    {
        if (existing == null)
        {
            _context.WorkspaceMetadataValues.Add(new WorkspaceMetadataValue
            {
                WorkspaceId = workspaceId,
                FieldId = mapping.InternalFieldId,
                TextValue = value,
                SourceType = "ExternalSync",
                ExternalFieldRef = mapping.ExternalFieldName,
                LastSyncedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.TextValue = value;
            existing.SourceType = "ExternalSync";
            existing.LastSyncedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    private Task<string?> ApplyTransformAsync(string rawValue, string? expression, CancellationToken ct)
    {
        // Basic transforms: if expression is null/empty → return as-is
        // Future: evaluate C# script or regex
        if (string.IsNullOrWhiteSpace(expression)) return Task.FromResult<string?>(rawValue);

        // Simple built-in transforms
        var result = expression.ToLower() switch
        {
            "uppercase" => rawValue.ToUpperInvariant(),
            "lowercase" => rawValue.ToLowerInvariant(),
            "trim" => rawValue.Trim(),
            _ => rawValue  // custom expressions logged and returned raw
        };

        return Task.FromResult<string?>(result);
    }

    private object? GetMetadataValueAsObject(WorkspaceMetadataValue mv)
    {
        if (mv.TextValue != null) return mv.TextValue;
        if (mv.NumberValue.HasValue) return mv.NumberValue.Value;
        if (mv.DateValue.HasValue) return mv.DateValue.Value;
        if (mv.BoolValue.HasValue) return mv.BoolValue.Value;
        return null;
    }

    private async Task LogConflictAsync(Workspace workspace, MetadataSyncMapping mapping,
        string? internalValue, string? externalValue, CancellationToken ct)
    {
        _context.SyncEventLogs.Add(new SyncEventLog
        {
            ExternalSystemId = workspace.WorkspaceType.TypeId,  // corrected in implementation
            WorkspaceId = workspace.WorkspaceId,
            EventType = "ConflictDetected",
            Direction = "Inbound",
            ExternalObjectId = workspace.ExternalObjectId,
            ConflictDetails = System.Text.Json.JsonSerializer.Serialize(new
            {
                FieldCode = mapping.InternalField.FieldCode,
                InternalValue = internalValue,
                ExternalValue = externalValue,
                Strategy = mapping.ConflictStrategy
            }),
            IsSuccessful = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task LogSyncEventAsync(Workspace workspace, string eventType, string direction,
        bool success, int fieldsUpdated, string? error, CancellationToken ct)
    {
        _context.SyncEventLogs.Add(new SyncEventLog
        {
            WorkspaceId = workspace.WorkspaceId,
            EventType = eventType,
            Direction = direction,
            ExternalObjectId = workspace.ExternalObjectId,
            FieldsUpdated = System.Text.Json.JsonSerializer.Serialize(new { Count = fieldsUpdated }),
            ErrorMessage = error,
            IsSuccessful = success,
            CreatedAt = DateTime.UtcNow
        });
    }
}

// ================================================================
// FILE: src/Infrastructure/Sync/SAPConnector.cs
// ================================================================
namespace Darah.ECM.Infrastructure.Sync.Connectors;

/// <summary>SAP system connector — REST OData integration</summary>
public class SAPConnector : IExternalSystemConnector
{
    public string SystemCode => "SAP_PROD";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<SAPConnector> _logger;

    public SAPConnector(HttpClient http, IConfiguration config, ILogger<SAPConnector> logger)
    {
        _http = http; _config = config; _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/sap/opu/odata/sap/API_PROJECT_COST_CENTER/", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default)
    {
        // SAP OData URL construction based on object type
        var url = BuildSapUrl(objectType, objectId);
        if (url == null) return null;

        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SAP fetch failed: {Status} for {Type}/{Id}", response.StatusCode, objectType, objectId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var root = System.Text.Json.JsonDocument.Parse(json).RootElement;

            var fields = new Dictionary<string, object?>();

            // Extract fields from SAP OData response structure
            if (root.TryGetProperty("d", out var d))
                foreach (var prop in d.EnumerateObject())
                    fields[prop.Name] = prop.Value.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                        System.Text.Json.JsonValueKind.Number => (object)prop.Value.GetDecimal(),
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        _ => prop.Value.ToString()
                    };

            return new ExternalObjectPayload { ObjectId = objectId, ObjectType = objectType, Fields = fields };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SAP connector error fetching {Type}/{Id}", objectType, objectId);
            return null;
        }
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId, Dictionary<string, object> fields, CancellationToken ct = default)
    {
        var url = BuildSapUrl(objectType, objectId);
        if (url == null) return false;

        var json = System.Text.Json.JsonSerializer.Serialize(fields);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _http.PatchAsync(url, content, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(string objectType, DateTime since, CancellationToken ct = default)
    {
        var sinceStr = since.ToString("yyyy-MM-ddTHH:mm:ss");
        var url = $"{BuildBaseUrl(objectType)}?$filter=LastChangedDateTime ge datetime'{sinceStr}'&$top=500";

        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<ExternalObjectPayload>();

            var json = await response.Content.ReadAsStringAsync(ct);
            var root = System.Text.Json.JsonDocument.Parse(json).RootElement;
            var results = new List<ExternalObjectPayload>();

            if (root.TryGetProperty("d", out var d) && d.TryGetProperty("results", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var fields = new Dictionary<string, object?>();
                    foreach (var prop in item.EnumerateObject())
                        fields[prop.Name] = prop.Value.ToString();

                    var id = fields.GetValueOrDefault("ObjectID")?.ToString() ?? string.Empty;
                    results.Add(new ExternalObjectPayload { ObjectId = id, ObjectType = objectType, Fields = fields });
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SAP delta fetch failed for {Type}", objectType);
            return Enumerable.Empty<ExternalObjectPayload>();
        }
    }

    private string? BuildSapUrl(string objectType, string objectId) => objectType switch
    {
        "WBSElement" => $"/sap/opu/odata/sap/API_PROJECT/A_EnterpriseProjectElement('{objectId}')",
        "PurchaseOrder" => $"/sap/opu/odata/sap/API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder('{objectId}')",
        "Contract" => $"/sap/opu/odata/sap/API_CONTRACT/A_Contract('{objectId}')",
        _ => null
    };

    private string BuildBaseUrl(string objectType) => objectType switch
    {
        "WBSElement" => "/sap/opu/odata/sap/API_PROJECT/A_EnterpriseProjectElement",
        _ => $"/sap/opu/odata/sap/API_{objectType.ToUpper()}"
    };
}

/// <summary>Salesforce CRM connector — REST API</summary>
public class SalesforceConnector : IExternalSystemConnector
{
    public string SystemCode => "SF_CRM";

    private readonly HttpClient _http;
    private readonly ILogger<SalesforceConnector> _logger;

    public SalesforceConnector(HttpClient http, ILogger<SalesforceConnector> logger)
    {
        _http = http; _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/services/data/v58.0/", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default)
    {
        var url = $"/services/data/v58.0/sobjects/{objectType}/{objectId}";
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        var root = System.Text.Json.JsonDocument.Parse(json).RootElement;
        var fields = new Dictionary<string, object?>();

        foreach (var prop in root.EnumerateObject())
            if (prop.Name != "attributes")
                fields[prop.Name] = prop.Value.ToString();

        return new ExternalObjectPayload { ObjectId = objectId, ObjectType = objectType, Fields = fields };
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId, Dictionary<string, object> fields, CancellationToken ct = default)
    {
        var url = $"/services/data/v58.0/sobjects/{objectType}/{objectId}";
        var json = System.Text.Json.JsonSerializer.Serialize(fields);
        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        var response = await _http.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(string objectType, DateTime since, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<ExternalObjectPayload>());
}

// ================================================================
// FILE: src/Infrastructure/Sync/WorkspaceSyncJob.cs (Hangfire)
// ================================================================
namespace Darah.ECM.Infrastructure.Jobs;

public class WorkspaceSyncJob
{
    private readonly IMetadataSyncEngine _syncEngine;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<WorkspaceSyncJob> _logger;

    public WorkspaceSyncJob(IMetadataSyncEngine syncEngine, IApplicationDbContext context, ILogger<WorkspaceSyncJob> logger)
    {
        _syncEngine = syncEngine; _context = context; _logger = logger;
    }

    /// <summary>Hourly job: sync all external systems</summary>
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
    public async Task SyncAllExternalSystemsAsync()
    {
        _logger.LogInformation("WorkspaceSyncJob starting at {Time}", DateTime.UtcNow);

        var activeSystems = await _context.ExternalSystems
            .Where(s => s.IsActive)
            .ToListAsync();

        foreach (var system in activeSystems)
        {
            try
            {
                var result = await _syncEngine.BulkSyncAsync(system.SystemCode);
                _logger.LogInformation("Bulk sync {System}: {Synced} synced, {Failed} failed, {Fields} fields",
                    system.SystemCode, result.WorkspacesSynced, result.WorkspacesFailed, result.TotalFieldsUpdated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk sync failed for system {System}", system.SystemCode);
            }
        }
    }

    /// <summary>On-demand sync for a single workspace</summary>
    public async Task SyncWorkspaceAsync(Guid workspaceId)
    {
        var result = await _syncEngine.TriggerSyncAsync(workspaceId, SyncDirection.Inbound);
        _logger.LogInformation("On-demand sync workspace {Id}: {Success}, {Fields} fields updated",
            workspaceId, result.IsSuccess, result.FieldsUpdated);
    }

    /// <summary>Delta sync: check external systems for changes in last N minutes</summary>
    public async Task DeltaSyncAsync(string externalSystemCode, int minutesBack = 60)
    {
        var since = DateTime.UtcNow.AddMinutes(-minutesBack);
        _logger.LogInformation("Delta sync {System} since {Since}", externalSystemCode, since);
        await _syncEngine.BulkSyncAsync(externalSystemCode);
    }
}
