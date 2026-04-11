using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Application.Common.Correlation;

// ─── CORRELATION CONTEXT ──────────────────────────────────────────────────────
/// <summary>
/// Provides a consistent correlation ID throughout a request's lifetime.
/// Used by all audit log writes, structured log entries, and error responses.
/// The same correlationId appears in: AuditLogs, structured logs (Serilog), API response header.
///
/// CONSISTENCY RULE: Every audit log entry MUST include the CorrelationId.
/// This enables reconstructing the full timeline of any operation.
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
    string? UserId       { get; }
    string? Username     { get; }
    string? IPAddress    { get; }
}

public sealed class HttpCorrelationContext : ICorrelationContext
{
    private readonly IHttpContextAccessor _http;
    private readonly ICurrentUser         _currentUser;

    public HttpCorrelationContext(IHttpContextAccessor http, ICurrentUser currentUser)
        { _http = http; _currentUser = currentUser; }

    public string CorrelationId =>
        _http.HttpContext?.Items["CorrelationId"]?.ToString()
        ?? _http.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");

    public string? UserId    => _currentUser.IsAuthenticated ? _currentUser.UserId.ToString() : null;
    public string? Username  => _currentUser.IsAuthenticated ? _currentUser.Username : null;
    public string? IPAddress => _currentUser.IPAddress;
}

// ─── STRUCTURED AUDIT ENTRY ──────────────────────────────────────────────────
/// <summary>
/// Uniform audit entry model used by ALL modules.
/// Ensures every audit record has the same fields regardless of which module wrote it.
///
/// MANDATORY FIELDS (always present):
///   - CorrelationId  → links all log lines in a single request
///   - EventType      → business event name (e.g., "DocumentCreated", "WorkflowApproved")
///   - UserId         → who performed the action
///   - Timestamp      → when it happened (UTC)
///   - IsSuccessful   → outcome
///
/// OPTIONAL FIELDS (set when relevant):
///   - EntityType     → what entity was affected ("Document", "WorkflowTask", etc.)
///   - EntityId       → the entity's identifier
///   - OldValues      → JSON snapshot before change
///   - NewValues      → JSON snapshot after change
/// </summary>
public sealed class AuditEntry
{
    public string   CorrelationId  { get; init; } = string.Empty;
    public string   EventType      { get; init; } = string.Empty;
    public string?  EntityType     { get; init; }
    public string?  EntityId       { get; init; }
    public string   Module         { get; init; } = string.Empty;  // Documents|Workflow|Records|Metadata|Admin
    public int?     UserId         { get; init; }
    public string?  Username       { get; init; }
    public string?  IPAddress      { get; init; }
    public object?  OldValues      { get; init; }
    public object?  NewValues      { get; init; }
    public string?  AdditionalInfo { get; init; }
    public string   Severity       { get; init; } = "Info";  // Info|Warning|Error|Critical
    public bool     IsSuccessful   { get; init; } = true;
    public string?  FailureReason  { get; init; }
    public DateTime Timestamp      { get; init; } = DateTime.UtcNow;

    // Predefined module constants
    public static class Modules
    {
        public const string Documents = "Documents";
        public const string Workflow  = "Workflow";
        public const string Records   = "Records";
        public const string Metadata  = "Metadata";
        public const string Folders   = "Folders";
        public const string Auth      = "Auth";
        public const string Admin     = "Admin";
        public const string System    = "System";
    }
}

// ─── STRUCTURED AUDIT SERVICE WRAPPER ────────────────────────────────────────
/// <summary>
/// Wraps IAuditService to enforce the AuditEntry structure with correlation ID.
/// All modules must use this instead of calling IAuditService directly.
/// </summary>
public interface IStructuredAuditService
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
    Task LogSuccessAsync(string eventType, string module, string? entityType, string? entityId,
        object? newValues = null, object? oldValues = null,
        string? additionalInfo = null, CancellationToken ct = default);
    Task LogFailureAsync(string eventType, string module, string? entityType, string? entityId,
        string failureReason, string severity = "Warning",
        CancellationToken ct = default);
    Task LogSecurityEventAsync(string eventType, string module,
        string description, string severity = "Warning",
        CancellationToken ct = default);
}

public sealed class StructuredAuditService : IStructuredAuditService
{
    private readonly IAuditService       _auditService;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<StructuredAuditService> _logger;

    public StructuredAuditService(IAuditService auditService,
        ICorrelationContext correlation, ILogger<StructuredAuditService> logger)
    {
        _auditService = auditService;
        _correlation  = correlation;
        _logger       = logger;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        // Enrich with correlation context if not already set
        var enriched = entry with
        {
            CorrelationId = string.IsNullOrEmpty(entry.CorrelationId)
                ? _correlation.CorrelationId : entry.CorrelationId,
            UserId        = entry.UserId   ?? (int.TryParse(_correlation.UserId, out var uid) ? uid : null),
            Username      = entry.Username ?? _correlation.Username,
            IPAddress     = entry.IPAddress ?? _correlation.IPAddress,
            Timestamp     = entry.Timestamp == default ? DateTime.UtcNow : entry.Timestamp
        };

        // Structured log line (also goes to Serilog)
        _logger.LogInformation(
            "[AUDIT] {Module} | {EventType} | User={User} | Entity={EntityType}:{EntityId} | " +
            "Success={Success} | CorrelationId={CorrelationId}",
            enriched.Module, enriched.EventType, enriched.Username ?? enriched.UserId?.ToString(),
            enriched.EntityType, enriched.EntityId, enriched.IsSuccessful, enriched.CorrelationId);

        // Persist to AuditLogs table
        await _auditService.LogAsync(
            eventType:     enriched.EventType,
            entityType:    enriched.EntityType,
            entityId:      enriched.EntityId,
            oldValues:     enriched.OldValues,
            newValues:     enriched.NewValues,
            severity:      enriched.Severity,
            isSuccessful:  enriched.IsSuccessful,
            failureReason: enriched.FailureReason,
            additionalInfo: $"Module={enriched.Module} CorrelationId={enriched.CorrelationId}" +
                           (enriched.AdditionalInfo is not null ? $" {enriched.AdditionalInfo}" : ""),
            ct: ct);
    }

    public Task LogSuccessAsync(string eventType, string module,
        string? entityType, string? entityId,
        object? newValues = null, object? oldValues = null,
        string? additionalInfo = null, CancellationToken ct = default)
        => LogAsync(new AuditEntry
        {
            EventType      = eventType,
            Module         = module,
            EntityType     = entityType,
            EntityId       = entityId,
            NewValues      = newValues,
            OldValues      = oldValues,
            AdditionalInfo = additionalInfo,
            IsSuccessful   = true,
            Severity       = "Info"
        }, ct);

    public Task LogFailureAsync(string eventType, string module,
        string? entityType, string? entityId,
        string failureReason, string severity = "Warning",
        CancellationToken ct = default)
        => LogAsync(new AuditEntry
        {
            EventType      = eventType,
            Module         = module,
            EntityType     = entityType,
            EntityId       = entityId,
            IsSuccessful   = false,
            FailureReason  = failureReason,
            Severity       = severity
        }, ct);

    public Task LogSecurityEventAsync(string eventType, string module,
        string description, string severity = "Warning",
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "[SECURITY_AUDIT] {Module} | {EventType} | {Description} | " +
            "User={User} | IP={IP} | CorrelationId={CorrelationId}",
            module, eventType, description,
            _correlation.Username, _correlation.IPAddress, _correlation.CorrelationId);

        return LogAsync(new AuditEntry
        {
            EventType      = eventType,
            Module         = module,
            AdditionalInfo = description,
            IsSuccessful   = false,
            Severity       = severity
        }, ct);
    }
}
