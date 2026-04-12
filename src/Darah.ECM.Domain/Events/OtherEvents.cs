using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Events.Workflow
{
    public record WorkflowStartedEvent(int InstanceId, Guid DocumentId,
        int DefinitionId, int StartedBy) : DomainEvent
    { public override string EventType => nameof(WorkflowStartedEvent); }

    public record WorkflowCompletedEvent(int InstanceId, Guid DocumentId,
        int StartedBy) : DomainEvent
    { public override string EventType => nameof(WorkflowCompletedEvent); }

    public record WorkflowRejectedEvent(int InstanceId, Guid DocumentId,
        int RejectedBy, string? Reason) : DomainEvent
    { public override string EventType => nameof(WorkflowRejectedEvent); }

    public record SLABreachedEvent(int TaskId, int InstanceId,
        int? AssignedToUserId, DateTime DueAt) : DomainEvent
    { public override string EventType => nameof(SLABreachedEvent); }
}

namespace Darah.ECM.Domain.Events.Records
{
    public record RecordDeclaredEvent(Guid DocumentId, string DocumentNumber,
        int RecordClassId, int DeclaredBy) : DomainEvent
    { public override string EventType => nameof(RecordDeclaredEvent); }
}
