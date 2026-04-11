using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;

namespace Darah.ECM.Infrastructure.Persistence.Outbox;

// ─── OUTBOX MESSAGE ENTITY ────────────────────────────────────────────────────
/// <summary>
/// Persistent record of a domain event that needs to be dispatched.
/// Stored in the same DB transaction as the business data — guarantees
/// at-least-once delivery even if the process crashes after commit.
///
/// IDEMPOTENCY: Consumers must handle duplicate delivery using ProcessedEventId tracking.
/// RETRY: Hangfire processes failed outbox messages with exponential backoff.
/// DEAD LETTER: After MaxAttempts failures, message is marked Failed for manual review.
/// </summary>
public sealed class OutboxMessage
{
    public Guid     MessageId      { get; private set; } = Guid.NewGuid();
    public string   EventType      { get; private set; } = string.Empty;
    public string   EventPayload   { get; private set; } = string.Empty; // JSON
    public DateTime CreatedAt      { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt   { get; private set; }
    public int      AttemptCount   { get; private set; } = 0;
    public DateTime? NextRetryAt   { get; private set; }
    public string   Status         { get; private set; } = "Pending"; // Pending|Processing|Processed|Failed
    public string?  LastError      { get; private set; }
    public string?  CorrelationId  { get; private set; }

    public const int MaxAttempts = 5;

    private OutboxMessage() { }

    public static OutboxMessage Create(string eventType, string payload, string? correlationId = null)
        => new()
        {
            MessageId     = Guid.NewGuid(),
            EventType     = eventType,
            EventPayload  = payload,
            CreatedAt     = DateTime.UtcNow,
            Status        = "Pending",
            CorrelationId = correlationId
        };

    public void MarkProcessing() => Status = "Processing";

    public void MarkProcessed()
    {
        Status      = "Processed";
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        AttemptCount++;
        LastError = error;

        if (AttemptCount >= MaxAttempts)
        {
            Status = "Failed"; // Dead-lettered
        }
        else
        {
            // Exponential backoff: 30s, 2m, 10m, 30m, 2h
            var delaySeconds = (int)Math.Pow(4, AttemptCount) * 30;
            NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            Status      = "Pending";
        }
    }
}

// ─── OUTBOX PROCESSOR JOB ─────────────────────────────────────────────────────
/// <summary>
/// Hangfire background job that processes pending outbox messages.
/// Runs every 30 seconds. Ensures at-least-once event delivery.
/// </summary>
public sealed class OutboxProcessorJob
{
    private readonly EcmDbContext _ctx;
    private readonly IEventBus    _eventBus;
    private readonly ILogger<OutboxProcessorJob> _logger;

    public OutboxProcessorJob(EcmDbContext ctx, IEventBus eventBus,
        ILogger<OutboxProcessorJob> logger)
    {
        _ctx      = ctx;
        _eventBus = eventBus;
        _logger   = logger;
    }

    [AutomaticRetry(Attempts = 0)] // Outbox handles its own retry
    public async Task ExecuteAsync()
    {
        var messages = await _ctx.Set<OutboxMessage>()
            .Where(m => m.Status == "Pending"
                     && (m.NextRetryAt == null || m.NextRetryAt <= DateTime.UtcNow))
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync();

        if (!messages.Any()) return;

        _logger.LogDebug("OutboxProcessor: processing {Count} message(s)", messages.Count);

        foreach (var msg in messages)
        {
            msg.MarkProcessing();
            await _ctx.SaveChangesAsync();

            try
            {
                // Deserialize and publish the event
                // In full implementation: use a type registry to resolve the concrete event type
                // and deserialize with System.Text.Json
                await _eventBus.PublishAsync(new OutboxEventEnvelope(
                    msg.MessageId, msg.EventType, msg.EventPayload, msg.CorrelationId));

                msg.MarkProcessed();
                _logger.LogInformation("Outbox processed: {EventType} ({MsgId})",
                    msg.EventType, msg.MessageId);
            }
            catch (Exception ex)
            {
                msg.MarkFailed(ex.Message);
                _logger.LogError(ex, "Outbox failed: {EventType} ({MsgId}), attempt {Attempt}",
                    msg.EventType, msg.MessageId, msg.AttemptCount);
            }

            await _ctx.SaveChangesAsync();
        }
    }
}

/// <summary>Envelope for outbox-sourced events — carries deserialized payload metadata.</summary>
public sealed record OutboxEventEnvelope(
    Guid   MessageId,
    string EventType,
    string JsonPayload,
    string? CorrelationId);
