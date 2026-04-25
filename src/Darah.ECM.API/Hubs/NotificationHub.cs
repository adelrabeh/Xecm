using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Darah.ECM.API.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("uid")?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst("uid")?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task MarkRead(long notificationId) =>
        await Clients.Caller.SendAsync("NotificationRead", notificationId);
}

public interface INotificationSender
{
    Task SendToUserAsync(int userId, NotificationPayload payload);
    Task BroadcastAsync(NotificationPayload payload);
}

public sealed class NotificationSender : INotificationSender
{
    private readonly IHubContext<NotificationHub> _hub;
    public NotificationSender(IHubContext<NotificationHub> hub) => _hub = hub;
    public Task SendToUserAsync(int userId, NotificationPayload payload) =>
        _hub.Clients.Group($"user-{userId}").SendAsync("Notification", payload);
    public Task BroadcastAsync(NotificationPayload payload) =>
        _hub.Clients.All.SendAsync("Notification", payload);
}

public sealed record NotificationPayload(
    long Id, string Type, string TitleAr, string TitleEn,
    string BodyAr, string BodyEn, string? EntityId,
    string? NavigateTo, bool IsRead = false);
