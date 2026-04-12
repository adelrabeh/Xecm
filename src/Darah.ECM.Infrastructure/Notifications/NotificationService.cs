using Microsoft.Extensions.Logging;
using Darah.ECM.Application.Common.Interfaces;

namespace Darah.ECM.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(int userId, string title, string message, string type,
        string? link, string? icon, string? metadata, int priority, CancellationToken ct)
    {
        // Temporary implementation (no DB dependency)
        _logger.LogInformation("Notification → User {UserId} | {Title}", userId, title);
        return Task.CompletedTask;
    }

    public Task MarkReadAsync(long notificationId, int userId, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<IEnumerable<object>> GetUnreadAsync(int userId, CancellationToken ct)
    {
        return Task.FromResult<IEnumerable<object>>(new List<object>());
    }
}
