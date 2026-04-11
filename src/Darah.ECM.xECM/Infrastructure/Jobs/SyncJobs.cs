using Darah.ECM.xECM.Domain.Interfaces;
using Darah.ECM.xECM.Infrastructure.Sync;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.xECM.Infrastructure.Jobs;

/// <summary>
/// Scheduled sync job — runs every 30 minutes for all pending/failed workspaces.
/// Triggered by: Hangfire recurring job scheduler.
/// Processes workspaces in batches of 100, respects 5-attempt dead-letter limit.
/// </summary>
public sealed class SyncSchedulerJob
{
    private readonly IMetadataSyncEngine     _syncEngine;
    private readonly IExternalSystemRepository _systemRepo;
    private readonly ILogger<SyncSchedulerJob> _logger;

    public SyncSchedulerJob(IMetadataSyncEngine syncEngine,
        IExternalSystemRepository systemRepo, ILogger<SyncSchedulerJob> logger)
    { _syncEngine = syncEngine; _systemRepo = systemRepo; _logger = logger; }

    [AutomaticRetry(Attempts = 0)]   // Sync engine handles its own retry via workspace state
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Scheduled sync job started at {Time}", DateTime.UtcNow);
        var systems = await _systemRepo.GetActiveAsync();
        int totalSynced = 0, totalFailed = 0;

        foreach (var system in systems)
        {
            var result = await _syncEngine.BulkSyncAsync(system.SystemCode, SyncDirection.Inbound);
            totalSynced += result.WorkspacesSynced;
            totalFailed += result.WorkspacesFailed;
            _logger.LogInformation(
                "Sync batch {Code}: Synced={S} Failed={F} Fields={F2}",
                system.SystemCode, result.WorkspacesSynced, result.WorkspacesFailed, result.TotalFieldsUpdated);
        }

        _logger.LogInformation(
            "Scheduled sync complete. Total: Synced={Synced} Failed={Failed}",
            totalSynced, totalFailed);
    }
}

/// <summary>
/// Webhook event receiver — enqueues immediate sync when external system pushes a change.
/// Triggered by: HTTP POST to /api/v1/webhooks/sync/{systemCode}
/// Processed by Hangfire in background — webhook returns 202 immediately.
/// </summary>
public sealed class WebhookSyncJob
{
    private readonly IMetadataSyncEngine   _syncEngine;
    private readonly IWorkspaceRepository  _wsRepo;
    private readonly ILogger<WebhookSyncJob> _logger;

    public WebhookSyncJob(IMetadataSyncEngine syncEngine, IWorkspaceRepository wsRepo,
        ILogger<WebhookSyncJob> logger)
    { _syncEngine = syncEngine; _wsRepo = wsRepo; _logger = logger; }

    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessWebhookAsync(
        string systemCode, string objectId, string objectType,
        string eventType, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Webhook sync: {System}/{Type}/{Id} event={Event}",
            systemCode, objectType, objectId, eventType);

        // Find workspace bound to this external object
        var workspace = await _wsRepo.GetByExternalObjectAsync(systemCode, objectId, ct);
        if (workspace is null)
        {
            _logger.LogWarning(
                "No workspace found for {System}/{Id} — webhook ignored", systemCode, objectId);
            return;
        }

        if (!workspace.NeedsSync())
        {
            _logger.LogWarning(
                "Workspace {WsId} sync skipped (too many failures or not pending)", workspace.WorkspaceId);
            return;
        }

        var result = await _syncEngine.TriggerSyncAsync(
            workspace.WorkspaceId, SyncDirection.Inbound, "Webhook", ct);

        _logger.LogInformation(
            "Webhook sync result: WS={WsId} Success={S} Fields={F}",
            workspace.WorkspaceId, result.IsSuccess, result.FieldsUpdated);
    }
}

/// <summary>
/// Webhook receiver controller — accepts push events from external systems.
/// Immediately enqueues a background sync job and returns 202 Accepted.
/// </summary>
[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/v1/webhooks")]
public sealed class WebhookController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(IBackgroundJobClient jobs, ILogger<WebhookController> logger)
    { _jobs = jobs; _logger = logger; }

    /// <summary>
    /// Receive a push notification from an external system.
    /// Immediately returns 202; sync happens asynchronously via Hangfire.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.HttpPost("{systemCode}/events")]
    public Microsoft.AspNetCore.Mvc.IActionResult ReceiveEvent(
        string systemCode,
        [Microsoft.AspNetCore.Mvc.FromBody] WebhookPayload payload)
    {
        _logger.LogInformation(
            "Webhook received: {System}/{Type}/{Id} event={Event}",
            systemCode, payload.ObjectType, payload.ObjectId, payload.EventType);

        // Enqueue background job — do NOT process synchronously
        _jobs.Enqueue<WebhookSyncJob>(j =>
            j.ProcessWebhookAsync(systemCode, payload.ObjectId,
                payload.ObjectType, payload.EventType, CancellationToken.None));

        return Accepted(new { message = "Webhook received and queued for processing" });
    }
}

public sealed record WebhookPayload(
    string ObjectId,
    string ObjectType,
    string EventType,     // Created|Updated|Deleted|StatusChanged
    Dictionary<string, object>? ChangedFields = null);
