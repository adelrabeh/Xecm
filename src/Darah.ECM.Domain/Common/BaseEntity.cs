namespace Darah.ECM.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Carries audit fields, soft-delete, and a domain event collection.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public int CreatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public int? UpdatedBy { get; protected set; }
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }
    public int? DeletedBy { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent @event) => _domainEvents.Add(@event);
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void SetCreated(int userId) { CreatedBy = userId; CreatedAt = DateTime.UtcNow; }
    protected void SetUpdated(int userId) { UpdatedBy = userId; UpdatedAt = DateTime.UtcNow; }

    public void SoftDelete(int userId)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = userId;
        SetUpdated(userId);
    }
}

/// <summary>Marker interface for aggregate roots.</summary>
public interface IAggregateRoot { }

/// <summary>Marker interface for domain events.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}

/// <summary>Base record for domain events.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}
