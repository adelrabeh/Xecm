using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Darah.ECM.Application.Common.Interfaces;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;

namespace Darah.ECM.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly EcmDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(EcmDbContext db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SendAsync(int userId, string title, string message, string type,
        string? link, string? icon, string? metadata, int priority, CancellationToken ct)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Link = link,
            Icon = icon,
            Metadata = metadata,
            Priority = priority,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Notification sent to user {UserId}", userId);
    }

    public async Task MarkReadAsync(long notificationId, int userId, CancellationToken ct)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);

        if (notification == null) return;

        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<Notification>> GetUnreadAsync(int userId, CancellationToken ct)
    {
        return await _db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(ct);
    }
}
