namespace Darah.ECM.Infrastructure.Logging;

public sealed class AuditService : IAuditService
    {
        private readonly EcmDbContext _ctx;
        private readonly ICurrentUser _user;
        private readonly ILogger<AuditService> _log;
        public AuditService(EcmDbContext ctx, ICurrentUser user, ILogger<AuditService> log) { _ctx = ctx; _user = user; _log = log; }
    
        public async Task LogAsync(string eventType, string? entityType = null, string? entityId = null, object? oldValues = null, object? newValues = null, string severity = "Info", bool isSuccessful = true, string? failureReason = null, string? additionalInfo = null, CancellationToken ct = default)
        {
            try
            {
                var log = AuditLog.Create(eventType, entityType, entityId,
                    _user.IsAuthenticated ? _user.UserId : null,
                    _user.IsAuthenticated ? _user.Username : null,
                    _user.IPAddress,
                    oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                    newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
                    additionalInfo, severity, isSuccessful, failureReason, _user.SessionId);
                _ctx.AuditLogs.Add(log);
                await _ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex) { _log.LogError(ex, "Audit log failed for {EventType}", eventType); }
        }
    }
