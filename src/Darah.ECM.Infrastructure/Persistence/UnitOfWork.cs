using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.Persistence;

// ─── UNIT OF WORK ─────────────────────────────────────────────────────────────
/// <summary>
/// Unit of Work implementation.
///
/// DOMAIN EVENT DISPATCH CONTRACT:
///   Domain events are collected on entity objects during command execution.
///   They are dispatched ONLY after a successful CommitAsync() + CommitTransactionAsync(),
///   ensuring no event fires if the DB transaction rolls back.
///
///   Sequence for transactional commands:
///     1. BeginTransactionAsync()
///     2. ... perform operations, raise domain events on entities ...
///     3. CommitAsync()           ← persists to DB within transaction
///     4. CommitTransactionAsync() ← commits DB transaction
///     5. DispatchDomainEventsAsync() ← fires events ONLY after confirmed commit
///
///   For non-transactional commands (simple reads, single-table writes):
///     1. ... operate ...
///     2. CommitAsync()
///     3. DispatchDomainEventsAsync()
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly EcmDbContext   _ctx;
    private readonly IEventBus      _eventBus;
    private readonly ILogger<UnitOfWork> _logger;
    private IDbContextTransaction?  _tx;

    public UnitOfWork(
        EcmDbContext               ctx,
        IDocumentRepository        documents,
        IDocumentVersionRepository documentVersions,
        IUserRepository            users,
        IWorkflowRepository        workflows,
        IEventBus                  eventBus,
        ILogger<UnitOfWork>        logger)
    {
        _ctx             = ctx;
        Documents        = documents;
        DocumentVersions = documentVersions;
        Users            = users;
        Workflows        = workflows;
        _eventBus        = eventBus;
        _logger          = logger;
    }

    public IDocumentRepository        Documents        { get; }
    public IDocumentVersionRepository DocumentVersions { get; }
    public IUserRepository            Users            { get; }
    public IWorkflowRepository        Workflows        { get; }

    /// <summary>Persist changes to the database. Does NOT dispatch domain events.</summary>
    public async Task<int> CommitAsync(CancellationToken ct = default)
        => await _ctx.SaveChangesAsync(ct);

    /// <summary>
    /// Collect all pending domain events from tracked entities and publish them via IEventBus.
    /// Called AFTER CommitAsync() + CommitTransactionAsync() to guarantee events reflect committed state.
    /// Clears events from entities after dispatch.
    /// </summary>
    public async Task DispatchDomainEventsAsync(CancellationToken ct = default)
    {
        // Harvest events from all tracked BaseEntity instances
        var entitiesWithEvents = _ctx.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var allEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        if (!allEvents.Any()) return;

        _logger.LogDebug("Dispatching {Count} domain event(s) after commit", allEvents.Count);

        // Clear first so re-entrant saves during event handling don't re-dispatch
        foreach (var entity in entitiesWithEvents)
            entity.ClearDomainEvents();

        foreach (var @event in allEvents)
        {
            try
            {
                await _eventBus.PublishAsync(@event, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch domain event {EventType} ({EventId})",
                    @event.EventType, @event.EventId);
                // Event dispatch failures do NOT roll back the committed transaction.
                // The Outbox pattern will handle retries (Sprint 1+).
            }
        }
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _tx = await _ctx.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is not null) await _tx.CommitAsync(ct);
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is not null)
        {
            await _tx.RollbackAsync(ct);
            _logger.LogWarning("Transaction rolled back.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_tx is not null) await _tx.DisposeAsync();
        await _ctx.DisposeAsync();
    }
}
