using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Events.Workflow;

public record WorkflowStartedEvent(
    int  InstanceId,
    Guid DocumentId,
    int  DefinitionId,
    int  StartedBy) : DomainEvent
{
    public override string EventType => nameof(WorkflowStartedEvent);
}

public record WorkflowCompletedEvent(
    int  InstanceId,
    Guid DocumentId,
    int  StartedBy) : DomainEvent
{
    public override string EventType => nameof(WorkflowCompletedEvent);
}

public record WorkflowRejectedEvent(
    int     InstanceId,
    Guid    DocumentId,
    int     RejectedBy,
    string? Reason) : DomainEvent
{
    public override string EventType => nameof(WorkflowRejectedEvent);
}

public record SLABreachedEvent(
    int      TaskId,
    int      InstanceId,
    int?     AssignedToUserId,
    DateTime DueAt) : DomainEvent
{
    public override string EventType => nameof(SLABreachedEvent);
}

namespace Darah.ECM.Domain.Events.Records;

public record RecordDeclaredEvent(
    Guid   DocumentId,
    string DocumentNumber,
    int    RecordClassId,
    int    DeclaredBy) : DomainEvent
{
    public override string EventType => nameof(RecordDeclaredEvent);
}

namespace Darah.ECM.Domain.Events.Workspace;

public record WorkspaceCreatedEvent(
    Guid   WorkspaceId,
    string WorkspaceNumber,
    string WorkspaceTypeCode,
    int    CreatedBy) : DomainEvent
{
    public override string EventType => nameof(WorkspaceCreatedEvent);
}

public record WorkspaceLinkedToExternalEvent(
    Guid   WorkspaceId,
    string ExternalSystemId,
    string ExternalObjectId,
    string ExternalObjectType,
    int    LinkedBy) : DomainEvent
{
    public override string EventType => nameof(WorkspaceLinkedToExternalEvent);
}

public record WorkspaceArchivedEvent(
    Guid WorkspaceId,
    int  ArchivedBy) : DomainEvent
{
    public override string EventType => nameof(WorkspaceArchivedEvent);
}

public record WorkspaceLegalHoldAppliedEvent(
    Guid WorkspaceId,
    int  AppliedBy,
    int  AffectedDocumentCount) : DomainEvent
{
    public override string EventType => nameof(WorkspaceLegalHoldAppliedEvent);
}

public record MetadataSyncCompletedEvent(
    Guid   WorkspaceId,
    string ExternalSystemId,
    int    FieldsUpdated,
    int    ConflictsDetected) : DomainEvent
{
    public override string EventType => nameof(MetadataSyncCompletedEvent);
}

public record MetadataSyncFailedEvent(
    Guid   WorkspaceId,
    string ExternalSystemId,
    string ErrorMessage) : DomainEvent
{
    public override string EventType => nameof(MetadataSyncFailedEvent);
}
