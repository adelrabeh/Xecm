using MassTransit;

namespace Darah.ECM.Infrastructure.Messaging;

// ─── Shared Event Contracts ───────────────────────────────────────────────────
// These records define the message schema across all microservices

public record DocumentUploadedEvent(
    Guid DocumentId,
    string TitleAr,
    string? TitleEn,
    string StoragePath,
    string ContentType,
    long FileSizeBytes,
    int UploadedBy,
    DateTime UploadedAt);

public record DocumentIndexedEvent(
    Guid DocumentId,
    string TitleAr,
    string? TitleEn,
    string? ExtractedText,
    string Status,
    string? DocumentType,
    string? Language,
    DateTime CreatedAt,
    int CreatedBy,
    string[]? Tags);

public record DocumentDeletedEvent(Guid DocumentId, int DeletedBy);

public record OcrRequestedEvent(
    Guid DocumentId,
    string StoragePath,
    string ContentType);

public record OcrCompletedEvent(
    Guid DocumentId,
    bool Success,
    string ExtractedText,
    string Language,
    double Confidence,
    IReadOnlyDictionary<string, string> Metadata,
    DateTime CompletedAt);

public record WorkflowTriggeredEvent(
    Guid DocumentId,
    int DefinitionId,
    int StartedBy,
    int Priority);

public record WorkflowTaskAssignedEvent(
    int TaskId,
    int AssignedToUserId,
    string DocumentTitle,
    DateTime DueDate);

// ─── MassTransit Event Publisher ─────────────────────────────────────────────
public interface IEventPublisher
{
    Task PublishDocumentUploadedAsync(DocumentUploadedEvent evt, CancellationToken ct);
    Task PublishDocumentDeletedAsync(DocumentDeletedEvent evt, CancellationToken ct);
    Task RequestOcrAsync(OcrRequestedEvent evt, CancellationToken ct);
    Task PublishWorkflowTriggeredAsync(WorkflowTriggeredEvent evt, CancellationToken ct);
}

public sealed class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<MassTransitEventPublisher> _log;

    public MassTransitEventPublisher(IPublishEndpoint bus,
        ILogger<MassTransitEventPublisher> log)
    { _bus = bus; _log = log; }

    public async Task PublishDocumentUploadedAsync(DocumentUploadedEvent evt,
        CancellationToken ct)
    {
        await _bus.Publish(evt, ct);
        _log.LogInformation("Published DocumentUploaded for {DocId}", evt.DocumentId);
    }

    public Task PublishDocumentDeletedAsync(DocumentDeletedEvent evt,
        CancellationToken ct) => _bus.Publish(evt, ct);

    public async Task RequestOcrAsync(OcrRequestedEvent evt, CancellationToken ct)
    {
        await _bus.Publish(evt, ct);
        _log.LogInformation("OCR requested for {DocId}", evt.DocumentId);
    }

    public Task PublishWorkflowTriggeredAsync(WorkflowTriggeredEvent evt,
        CancellationToken ct) => _bus.Publish(evt, ct);
}

// ─── MassTransit Registration ─────────────────────────────────────────────────
public static class MassTransitExtensions
{
    public static IServiceCollection AddEcmMessageBus(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddMassTransit(x =>
        {
            // Core API consumers

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var host = config["RabbitMQ:Host"] ?? "localhost";
                cfg.Host(host, "/", h =>
                {
                    h.Username(config["RabbitMQ:Username"] ?? "guest");
                    h.Password(config["RabbitMQ:Password"] ?? "guest");
                    h.Heartbeat(TimeSpan.FromSeconds(10));
                    h.RequestedConnectionTimeout(TimeSpan.FromSeconds(30));
                });

                // Configure exchanges for each event type
                cfg.Message<DocumentUploadedEvent>(m =>
                    m.SetEntityName("ecm.document.uploaded"));
                cfg.Message<OcrRequestedEvent>(m =>
                    m.SetEntityName("ecm.ocr.requested"));
                cfg.Message<WorkflowTriggeredEvent>(m =>
                    m.SetEntityName("ecm.workflow.triggered"));

                cfg.UseMessageRetry(r => r.Exponential(5,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromSeconds(5)));

                cfg.UseInMemoryOutbox(ctx);
                cfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        return services;
    }
}