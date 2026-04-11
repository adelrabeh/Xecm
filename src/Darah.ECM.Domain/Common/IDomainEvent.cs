namespace Darah.ECM.Domain.Common;

/// <summary>Marker interface for all domain events.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}

/// <summary>Base record — all domain events derive from this.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}

/// <summary>Marker: only aggregate roots may be persisted directly via repositories.</summary>
public interface IAggregateRoot { }
