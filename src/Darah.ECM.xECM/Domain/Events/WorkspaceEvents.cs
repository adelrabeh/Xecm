using Darah.ECM.Domain.Common;

namespace Darah.ECM.xECM.Domain.Events;

public record WorkspaceCreatedEvent(
    Guid   WorkspaceId, string WorkspaceNumber, string TitleAr,
    int    WorkspaceTypeId, int CreatedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceCreatedEvent); }

public record WorkspaceActivatedEvent(Guid WorkspaceId, int ActivatedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceActivatedEvent); }

public record WorkspaceClosedEvent(Guid WorkspaceId, int ClosedBy, string? Reason) : DomainEvent
{ public override string EventType => nameof(WorkspaceClosedEvent); }

public record WorkspaceArchivedEvent(Guid WorkspaceId, int ArchivedBy, int DocumentCount) : DomainEvent
{ public override string EventType => nameof(WorkspaceArchivedEvent); }

public record WorkspaceDisposedEvent(Guid WorkspaceId, int DisposedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceDisposedEvent); }

public record WorkspaceLegalHoldAppliedEvent(
    Guid WorkspaceId, int AppliedBy, int AffectedDocuments) : DomainEvent
{ public override string EventType => nameof(WorkspaceLegalHoldAppliedEvent); }

public record WorkspaceLegalHoldReleasedEvent(Guid WorkspaceId, int ReleasedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceLegalHoldReleasedEvent); }

public record WorkspaceDocumentBoundEvent(
    Guid WorkspaceId, Guid DocumentId, string BindingType, int AddedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceDocumentBoundEvent); }

public record WorkspaceDocumentRemovedEvent(
    Guid WorkspaceId, Guid DocumentId, int RemovedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceDocumentRemovedEvent); }

public record WorkspaceLinkedToExternalEvent(
    Guid WorkspaceId, string ExternalSystemCode, string ExternalObjectId,
    string ExternalObjectType, int LinkedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceLinkedToExternalEvent); }

public record WorkspaceExternalBindingRemovedEvent(Guid WorkspaceId, int RemovedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceExternalBindingRemovedEvent); }

public record WorkspaceSyncCompletedEvent(
    Guid WorkspaceId, string ExternalSystemCode,
    int FieldsUpdated, int ConflictsDetected) : DomainEvent
{ public override string EventType => nameof(WorkspaceSyncCompletedEvent); }

public record WorkspaceSyncFailedEvent(
    Guid WorkspaceId, string ExternalSystemCode, string ErrorMessage) : DomainEvent
{ public override string EventType => nameof(WorkspaceSyncFailedEvent); }

public record WorkspaceClassificationChangedEvent(
    Guid WorkspaceId, string NewClassificationCode, int ChangedBy) : DomainEvent
{ public override string EventType => nameof(WorkspaceClassificationChangedEvent); }

public record WorkspaceConflictDetectedEvent(
    Guid WorkspaceId, int FieldId, string ExternalValue,
    string InternalValue, string Strategy) : DomainEvent
{ public override string EventType => nameof(WorkspaceConflictDetectedEvent); }

public record SyncConflictResolvedEvent(
    Guid WorkspaceId, int FieldId, string Resolution, int ResolvedBy) : DomainEvent
{ public override string EventType => nameof(SyncConflictResolvedEvent); }
