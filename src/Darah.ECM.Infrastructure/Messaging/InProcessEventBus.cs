using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.Messaging;

/// <summary>
/// In-process synchronous event bus.
///
/// CURRENT PHASE: Modular Monolith — all handlers execute in the same process.
/// NEXT PHASE: Replace with MassTransit + RabbitMQ/Azure Service Bus by swapping
///             this class without touching any IEventHandler implementations or callers.
///
/// IDEMPOTENCY STRATEGY:
///   Each event carries a stable EventId (Guid). Handlers that perform non-idempotent
///   operations (e.g., email, external API calls) should record EventId in a
///   ProcessedEvents table and skip if already processed.
///
/// FAILURE POLICY:
///   - Handler exceptions are caught, logged as errors, and do NOT propagate.
///   - This prevents a failing notification handler from rolling back a document save.
///   - Critical handlers that MUST succeed should be moved to outbox pattern
///     (persist event in same DB transaction, process asynchronously via Hangfire).
///
/// RETRY:
///   - In-process bus does not retry. For retry semantics, use the Hangfire outbox pattern.
///   - MassTransit phase will add configurable retry/circuit-breaker middleware.
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessEventBus> _logger;

    public InProcessEventBus(IServiceProvider serviceProvider, ILogger<InProcessEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
    {
        var eventTypeName = typeof(T).Name;

        using var scope   = _serviceProvider.CreateScope();
        var handlers      = scope.ServiceProvider.GetServices<IEventHandler<T>>().ToList();

        if (!handlers.Any())
        {
            _logger.LogDebug("No handlers registered for event {EventType}", eventTypeName);
            return;
        }

        _logger.LogDebug("Publishing {EventType} to {Count} handler(s)", eventTypeName, handlers.Count);

        foreach (var handler in handlers)
        {
            var handlerName = handler.GetType().Name;
            try
            {
                await handler.HandleAsync(@event, ct);
                _logger.LogDebug("Handler {Handler} completed for {EventType}", handlerName, eventTypeName);
            }
            catch (Exception ex)
            {
                // Isolate handler failures — one bad handler must not break others or the command.
                _logger.LogError(ex,
                    "Event handler {Handler} failed for {EventType}. Event will NOT be retried in-process.",
                    handlerName, eventTypeName);

                // TODO (Outbox Phase): persist failed event envelope for Hangfire retry.
                // await _outboxRepository.RecordFailureAsync(@event, ex.Message, ct);
            }
        }
    }
}
