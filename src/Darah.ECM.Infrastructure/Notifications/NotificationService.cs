using Darah.ECM.Application.Notifications;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Darah.ECM.Infrastructure.Notifications;

/// <summary>
/// Notification service — delivers in-app + email notifications.
///
/// In-App: persists Notification entity to DB; user reads from /api/v1/notifications
/// Email: sends via IEmailService (SMTP abstraction)
///
/// All notification failures are logged but do NOT propagate —
/// a notification failure must never break the business operation that triggered it.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly EcmDbContext  _ctx;
    private readonly IEmailService _email;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(EcmDbContext ctx, IEmailService email,
        IUserRepository userRepo, ILogger<NotificationService> logger)
    {
        _ctx     = ctx;
        _email   = email;
        _userRepo = userRepo;
        _logger  = logger;
    }

    public async Task NotifyAsync(int userId, string title, string body, string type,
        string? entityType = null, string? entityId = null,
        string? actionUrl = null, CancellationToken ct = default)
    {
        try
        {
            var notification = Notification.Create(
                userId, title, body, type, entityType, entityId, actionUrl);
            _ctx.Set<Notification>().Add(notification);
            await _ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist in-app notification for user {UserId}", userId);
        }
    }

    public async Task NotifyWorkflowTaskAssignedAsync(int assignedUserId,
        string documentTitle, string workflowName, string stepName,
        int taskId, CancellationToken ct = default)
    {
        var title = "مهمة جديدة بانتظارك";
        var body  = $"تم تعيين مهمة '{stepName}' في مسار '{workflowName}' للوثيقة: {documentTitle}";

        await NotifyAsync(assignedUserId, title, body, "WorkflowTask",
            "WorkflowTask", taskId.ToString(), $"/workflow/inbox/{taskId}", ct: ct);

        // Email notification
        await SendEmailSafeAsync(assignedUserId, title, BuildTaskEmailHtml(
            documentTitle, workflowName, stepName, taskId), ct);
    }

    public async Task NotifySLABreachAsync(int assignedUserId, string documentTitle,
        string workflowName, DateTime dueAt, CancellationToken ct = default)
    {
        var title = "⚠️ تنبيه: انتهت مهلة مهمتك";
        var body  = $"انتهت مهلة مهمتك في مسار '{workflowName}' للوثيقة: {documentTitle}. المهلة كانت: {dueAt:yyyy-MM-dd HH:mm}";

        await NotifyAsync(assignedUserId, title, body, "SLABreach",
            "WorkflowTask", null, "/workflow/inbox", priority: 4, ct: ct);

        await SendEmailSafeAsync(assignedUserId, title,
            $"<p><strong>تنبيه عاجل:</strong> {body}</p>", ct);
    }

    public async Task NotifyDocumentApprovedAsync(int documentOwnerUserId,
        string documentTitle, string documentNumber, CancellationToken ct = default)
    {
        var title = $"✅ الوثيقة {documentNumber} معتمدة";
        var body  = $"تمت الموافقة على الوثيقة: {documentTitle}";

        await NotifyAsync(documentOwnerUserId, title, body, "DocumentApproved",
            "Document", documentNumber, ct: ct);
        await SendEmailSafeAsync(documentOwnerUserId, title, $"<p>{body}</p>", ct);
    }

    public async Task NotifyDocumentRejectedAsync(int documentOwnerUserId,
        string documentTitle, string documentNumber,
        string? reason, CancellationToken ct = default)
    {
        var title = $"❌ الوثيقة {documentNumber} مرفوضة";
        var body  = $"تم رفض الوثيقة: {documentTitle}" +
                    (reason is not null ? $"\nالسبب: {reason}" : "");

        await NotifyAsync(documentOwnerUserId, title, body, "DocumentRejected",
            "Document", documentNumber, ct: ct);
        await SendEmailSafeAsync(documentOwnerUserId, title,
            $"<p>{body.Replace("\n", "<br>")}</p>", ct);
    }

    public async Task NotifyRetentionExpiringAsync(int ownerUserId,
        string documentNumber, DateOnly expiryDate, CancellationToken ct = default)
    {
        var daysLeft = expiryDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        var title    = $"⏰ مدة احتفاظ الوثيقة {documentNumber} تنتهي قريباً";
        var body     = $"ستنتهي مدة الاحتفاظ بعد {daysLeft} يوم بتاريخ {expiryDate}";

        await NotifyAsync(ownerUserId, title, body, "RetentionExpiring",
            "Document", documentNumber, ct: ct);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────
    private async Task SendEmailSafeAsync(int userId, string subject,
        string htmlBody, CancellationToken ct)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(userId, ct) as User;
            if (user is null || string.IsNullOrEmpty(user.Email)) return;

            await _email.SendAsync(user.Email, user.FullNameAr,
                subject, WrapInEmailTemplate(subject, htmlBody), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email notification failed for user {UserId}", userId);
            // Never rethrow — email failure must not break the calling operation
        }
    }

    private static string BuildTaskEmailHtml(string docTitle, string wfName,
        string stepName, int taskId) => $"""
        <div style="font-family:Arial,sans-serif; direction:rtl; padding:20px;">
            <h2 style="color:#1B3A6B;">مهمة جديدة بانتظارك</h2>
            <table style="width:100%; border-collapse:collapse;">
                <tr><td style="padding:8px; font-weight:bold;">المستند:</td><td>{docTitle}</td></tr>
                <tr><td style="padding:8px; font-weight:bold;">مسار العمل:</td><td>{wfName}</td></tr>
                <tr><td style="padding:8px; font-weight:bold;">الخطوة:</td><td>{stepName}</td></tr>
            </table>
            <p><a href="/workflow/inbox/{taskId}" style="background:#1B3A6B;color:white;padding:10px 20px;text-decoration:none;border-radius:4px;">عرض المهمة</a></p>
        </div>
        """;

    private static string WrapInEmailTemplate(string title, string content) => $"""
        <!DOCTYPE html>
        <html dir="rtl">
        <head><meta charset="utf-8"><title>{title}</title></head>
        <body style="margin:0;padding:20px;background:#f5f7fa;">
            <div style="max-width:600px;margin:auto;background:white;border-radius:8px;overflow:hidden;">
                <div style="background:#1B3A6B;padding:20px;color:white;">
                    <h1 style="margin:0;font-size:20px;">نظام DARAH ECM</h1>
                </div>
                <div style="padding:20px;">{content}</div>
                <div style="background:#f5f7fa;padding:10px;text-align:center;font-size:12px;color:#666;">
                    دارة الملك عبدالعزيز — نظام إدارة المحتوى المؤسسي
                </div>
            </div>
        </body>
        </html>
        """;
}

// ─── SMTP EMAIL SERVICE ───────────────────────────────────────────────────────
public sealed class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        { _config = config; _logger = logger; }

    public async Task SendAsync(string toEmail, string toName, string subject,
        string htmlBody, CancellationToken ct = default)
    {
        var host     = _config["Email:SmtpHost"] ?? "localhost";
        var port     = int.Parse(_config["Email:SmtpPort"] ?? "587");
        var username = _config["Email:SmtpUsername"] ?? "";
        var password = _config["Email:SmtpPassword"] ?? "";
        var from     = _config["Email:FromAddress"] ?? "noreply@darah.gov.sa";
        var fromName = _config["Email:FromName"] ?? "DARAH ECM";

        try
        {
            using var client = new System.Net.Mail.SmtpClient(host, port)
            {
                Credentials = new System.Net.NetworkCredential(username, password),
                EnableSsl   = port == 465 || port == 587
            };

            var msg = new System.Net.Mail.MailMessage
            {
                From         = new System.Net.Mail.MailAddress(from, fromName),
                Subject      = subject,
                Body         = htmlBody,
                IsBodyHtml   = true,
                HeadersEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8,
                BodyEncoding    = System.Text.Encoding.UTF8
            };
            msg.To.Add(new System.Net.Mail.MailAddress(toEmail, toName));

            await client.SendMailAsync(msg, ct);
            _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP failed to {Email}: {Subject}", toEmail, subject);
            throw;
        }
    }

    public async Task SendTemplatedAsync(string toEmail, string templateCode,
        Dictionary<string, string> variables, CancellationToken ct = default)
    {
        // Full implementation: load template from NotificationTemplates table, replace variables
        var subject = variables.GetValueOrDefault("SUBJECT", templateCode);
        var body    = variables.Aggregate(
            $"<p>Template: {templateCode}</p>",
            (current, kvp) => current.Replace($"{{{kvp.Key}}}", kvp.Value));

        await SendAsync(toEmail, toEmail, subject, body, ct);
    }

    // ─── INotificationService implementation ─────────────────────────────────
    public async Task SendAsync(int userId, string titleAr, string body,
        string notificationType, string? entityType = null, string? entityId = null,
        string? actionUrl = null, int priority = 2, CancellationToken ct = default)
    {
        var notification = Darah.ECM.Domain.Entities.Notification.Create(
            userId, titleAr, body, notificationType,
            entityType, entityId, actionUrl, priority);
        _ctx.Set<Darah.ECM.Domain.Entities.Notification>().Add(notification);
        await _ctx.SaveChangesAsync(ct);
        _logger.LogDebug("Notification sent to user {UserId}: {Title}", userId, titleAr);
    }

    public async Task MarkReadAsync(long notificationId, int userId, CancellationToken ct = default)
    {
        var n = await _ctx.Set<Darah.ECM.Domain.Entities.Notification>()
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId, ct);
        if (n is not null) { n.MarkRead(); await _ctx.SaveChangesAsync(ct); }
    }

    public async Task<IEnumerable<Notification>> GetUnreadAsync(
        int userId, CancellationToken ct = default)
        => await _ctx.Set<Notification>()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

}