using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Events.Document;

public record DocumentCreatedEvent(
    Guid   DocumentId,
    string DocumentNumber,
    string TitleAr,
    int    CreatedBy) : DomainEvent
{
    public override string EventType => nameof(DocumentCreatedEvent);
}

public record DocumentApprovedEvent(
    Guid   DocumentId,
    string DocumentNumber,
    int    ApprovedBy) : DomainEvent
{
    public override string EventType => nameof(DocumentApprovedEvent);
}

public record DocumentArchivedEvent(
    Guid DocumentId,
    int  ArchivedBy) : DomainEvent
{
    public override string EventType => nameof(DocumentArchivedEvent);
}

public record DocumentCheckedOutEvent(
    Guid DocumentId,
    int  CheckedOutBy) : DomainEvent
{
    public override string EventType => nameof(DocumentCheckedOutEvent);
}

public record DocumentCheckedInEvent(
    Guid DocumentId,
    int  VersionId,
    int  CheckedInBy) : DomainEvent
{
    public override string EventType => nameof(DocumentCheckedInEvent);
}

public record LegalHoldAppliedEvent(
    Guid DocumentId,
    int  AppliedBy) : DomainEvent
{
    public override string EventType => nameof(LegalHoldAppliedEvent);
}

public record RetentionExpiredEvent(
    Guid     DocumentId,
    string   DocumentNumber,
    DateOnly ExpiredOn) : DomainEvent
{
    public override string EventType => nameof(RetentionExpiredEvent);
}
