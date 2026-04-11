namespace Darah.ECM.Domain.Common;

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

    public void RaiseDomainEvent(IDomainEvent @event) => _domainEvents.Add(@event);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public void SetCreated(int userId) { CreatedBy = userId; CreatedAt = DateTime.UtcNow; }
    public void SetUpdated(int userId) { UpdatedBy = userId; UpdatedAt = DateTime.UtcNow; }

    public void SoftDelete(int userId)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = userId;
        SetUpdated(userId);
    }
}
