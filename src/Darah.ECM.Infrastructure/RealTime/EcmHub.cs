using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Darah.ECM.Infrastructure.RealTime;

/// <summary>
/// SignalR Hub for real-time ECM collaboration.
/// Enables: document locking, live notifications, workflow updates.
/// </summary>
[Authorize]
public sealed class EcmHub : Hub
{
    private static readonly Dictionary<Guid, string> _documentLocks = new();

    public async Task LockDocument(Guid documentId)
    {
        var userId = Context.UserIdentifier!;
        lock (_documentLocks)
        {
            if (_documentLocks.TryGetValue(documentId, out var lockHolder) &&
                lockHolder != userId)
            {
                Clients.Caller.SendAsync("LockDenied", documentId, lockHolder);
                return;
            }
            _documentLocks[documentId] = userId;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId,
            $"doc_{documentId}");
        await Clients.Group($"doc_{documentId}")
            .SendAsync("DocumentLocked", documentId, userId);
    }

    public async Task UnlockDocument(Guid documentId)
    {
        lock (_documentLocks) _documentLocks.Remove(documentId);
        await Clients.Group($"doc_{documentId}")
            .SendAsync("DocumentUnlocked", documentId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            $"doc_{documentId}");
    }

    public async Task JoinWorkflowRoom(int workflowInstanceId)
        => await Groups.AddToGroupAsync(Context.ConnectionId,
            $"workflow_{workflowInstanceId}");

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Release all locks held by disconnected user
        var userId = Context.UserIdentifier;
        var released = _documentLocks
            .Where(kv => kv.Value == userId)
            .Select(kv => kv.Key).ToList();

        foreach (var docId in released)
        {
            _documentLocks.Remove(docId);
            await Clients.Group($"doc_{docId}")
                .SendAsync("DocumentUnlocked", docId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IEcmNotifier
{
    Task NotifyDocumentProcessed(Guid documentId);
    Task NotifyWorkflowTaskAssigned(int taskId, int userId);
    Task NotifyWorkflowCompleted(int instanceId);
}

public sealed class EcmNotifier : IEcmNotifier
{
    private readonly IHubContext<EcmHub> _hub;
    public EcmNotifier(IHubContext<EcmHub> hub) => _hub = hub;

    public Task NotifyDocumentProcessed(Guid documentId)
        => _hub.Clients.All.SendAsync("DocumentProcessed",
            new { documentId, timestamp = DateTime.UtcNow });

    public Task NotifyWorkflowTaskAssigned(int taskId, int userId)
        => _hub.Clients.User(userId.ToString())
            .SendAsync("TaskAssigned", new { taskId, timestamp = DateTime.UtcNow });

    public Task NotifyWorkflowCompleted(int instanceId)
        => _hub.Clients.Group($"workflow_{instanceId}")
            .SendAsync("WorkflowCompleted",
                new { instanceId, timestamp = DateTime.UtcNow });
}
