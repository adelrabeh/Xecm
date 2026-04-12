namespace Darah.ECM.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendAsync(
        int userId,
        string title,
        string message,
        string type,
        string? link,
        string? icon,
        string? metadata,
        int priority,
        CancellationToken ct);

    Task MarkReadAsync(
        long notificationId,
        int userId,
        CancellationToken ct);

    Task<IEnumerable<object>> GetUnreadAsync(
        int userId,
        CancellationToken ct);
}