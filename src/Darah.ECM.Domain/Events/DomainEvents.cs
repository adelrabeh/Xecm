using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Events;

// ─────────────────────────────────────────────────────────────
// DOCUMENT EVENTS
// ─────────────────────────────────────────────────────────────
public record DocumentCreatedEvent(
    Guid DocumentId,
    string DocumentNumber,
    string TitleAr,
    int CreatedBy) : DomainEvent
{
    public override string EventType => nameof(DocumentCreatedEvent);
}

public record DocumentApprovedEvent(
    Guid DocumentId,
    string DocumentNumber,
    int ApprovedBy) : DomainEvent
{
    public override string EventType => nameof(DocumentApprovedEvent);
}

public record DocumentArchivedEvent(
    Guid DocumentId,
    int ArchivedBy) : DomainEvent
{
    public override string EventType => nameof(DocumentArchivedEvent);
}

public record DocumentCheckedOutEvent(
    Guid DocumentId,
    int CheckedOutBy) : DomainEvent
{
    public override string EventType => nameof(DocumentCheckedOutEvent);
}

public record DocumentCheckedInEvent(
    Guid DocumentId,
    int VersionId,
    int CheckedInBy) : DomainEvent
{
    public override string EventType => nameof(DocumentCheckedInEvent);
}

public record LegalHoldAppliedEvent(
    Guid DocumentId,
    int AppliedBy) : DomainEvent
{
    public override string EventType => nameof(LegalHoldAppliedEvent);
}

public record RetentionExpiredEvent(
    Guid DocumentId,
    string DocumentNumber,
    DateOnly ExpiredOn) : DomainEvent
{
    public override string EventType => nameof(RetentionExpiredEvent);
}

// ─────────────────────────────────────────────────────────────
// WORKFLOW EVENTS
// ─────────────────────────────────────────────────────────────
public record WorkflowStartedEvent(
    int InstanceId,
    Guid DocumentId,
    int DefinitionId,
    int StartedBy) : DomainEvent
{
    public override string EventType => nameof(WorkflowStartedEvent);
}

public record WorkflowCompletedEvent(
    int InstanceId,
    Guid DocumentId,
    int StartedBy) : DomainEvent
{
    public override string EventType => nameof(WorkflowCompletedEvent);
}

public record WorkflowRejectedEvent(
    int InstanceId,
    Guid DocumentId,
    int RejectedBy,
    string? Reason) : DomainEvent
{
    public override string EventType => nameof(WorkflowRejectedEvent);
}

public record SLABreachedEvent(
    int TaskId,
    int InstanceId,
    int? AssignedToUserId,
    DateTime DueAt) : DomainEvent
{
    public override string EventType => nameof(SLABreachedEvent);
}

// ─────────────────────────────────────────────────────────────
// RECORDS EVENTS
// ─────────────────────────────────────────────────────────────
public record RecordDeclaredEvent(
    Guid DocumentId,
    string DocumentNumber,
    int RecordClassId,
    int DeclaredBy) : DomainEvent
{
    public override string EventType => nameof(RecordDeclaredEvent);
}

// ─────────────────────────────────────────────────────────────
// WORKSPACE EVENTS (xECM)
// ─────────────────────────────────────────────────────────────
public record WorkspaceCreatedEvent(
    Guid WorkspaceId,
    string WorkspaceNumber,
    string WorkspaceTypeCode,
    int CreatedBy) : DomainEvent
{
    public override string EventType => nameof(WorkspaceCreatedEvent);
}

public record WorkspaceLinkedToExternalEvent(
    Guid WorkspaceId,
    string ExternalSystemId,
    string ExternalObjectId,
    string ExternalObjectType,
    int LinkedBy) : DomainEvent
{
    public override string EventType => nameof(WorkspaceLinkedToExternalEvent);
}

public record WorkspaceArchivedEvent(
    Guid WorkspaceId,
    int ArchivedBy) : DomainEvent
{
    public override string EventType => nameof(WorkspaceArchivedEvent);
}

public record WorkspaceLegalHoldAppliedEvent(
    Guid WorkspaceId,
    int AppliedBy,
    int AffectedDocumentCount) : DomainEvent
{
    public override string EventType => nameof(WorkspaceLegalHoldAppliedEvent);
}

public record MetadataSyncCompletedEvent(
    Guid WorkspaceId,
    string ExternalSystemId,
    int FieldsUpdated,
    int ConflictsDetected) : DomainEvent
{
    public override string EventType => nameof(MetadataSyncCompletedEvent);
}

public record MetadataSyncFailedEvent(
    Guid WorkspaceId,
    string ExternalSystemId,
    string ErrorMessage) : DomainEvent
{
    public override string EventType => nameof(MetadataSyncFailedEvent);
}
