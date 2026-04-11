// ============================================================
// WORKFLOW COMMANDS & QUERIES
// ============================================================
namespace Darah.ECM.Application.Workflow.Commands;

using MediatR;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;

public record SubmitToWorkflowCommand(
    Guid DocumentId,
    int? WorkflowDefinitionId,
    int Priority = 2,
    string? Comment = null) : IRequest<ApiResponse<WorkflowInstanceDto>>;

public record WorkflowActionCommand(
    int TaskId,
    string ActionType,   // Approve | Reject | Return | Delegate | Comment
    string? Comment,
    int? DelegateToUserId) : IRequest<ApiResponse<bool>>;

public record WorkflowInstanceDto(
    int InstanceId,
    Guid DocumentId,
    string Status,
    string WorkflowNameAr,
    DateTime StartedAt,
    DateTime? CompletedAt,
    List<WorkflowTaskDto> Tasks);

public record WorkflowTaskDto(
    int TaskId,
    string StepNameAr,
    string StepNameEn,
    string? AssignedToNameAr,
    string Status,
    DateTime AssignedAt,
    DateTime? DueAt,
    bool IsOverdue,
    bool IsDelegated,
    List<WorkflowActionDto> Actions);

public record WorkflowActionDto(
    string ActionType,
    string? Comment,
    DateTime ActionAt,
    string? ActionByNameAr);

public record InboxItemDto(
    int TaskId,
    int InstanceId,
    Guid DocumentId,
    string DocumentTitleAr,
    string WorkflowNameAr,
    string StepNameAr,
    string Status,
    DateTime AssignedAt,
    DateTime? DueAt,
    bool IsOverdue,
    int Priority,
    string? DocumentTypeNameAr);

namespace Darah.ECM.Application.Workflow.Queries;

using MediatR;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Workflow.Commands;

public record GetWorkflowInboxQuery(
    string? Status = "Pending",
    bool OverdueOnly = false,
    int Page = 1,
    int PageSize = 20) : IRequest<ApiResponse<PagedResult<InboxItemDto>>>;

public record GetWorkflowTaskDetailQuery(int TaskId)
    : IRequest<ApiResponse<WorkflowTaskDto>>;

public record GetWorkflowHistoryQuery(int InstanceId)
    : IRequest<ApiResponse<List<WorkflowActionDto>>>;

// ============================================================
// SEARCH QUERY
// ============================================================
namespace Darah.ECM.Application.Search.Queries;

using MediatR;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Documents.DTOs;

public record AdvancedSearchQuery(
    string? TextQuery,
    int? DocumentTypeId,
    int? LibraryId,
    int? FolderId,
    int? StatusValueId,
    int? ClassificationOrder,
    DateTime? DateFrom,
    DateTime? DateTo,
    bool? IsLegalHold,
    List<int>? TagIds,
    Guid? WorkspaceId,
    string? ExternalSystemId,
    string? ExternalObjectId,
    string SortBy = "CreatedAt",
    string SortDir = "DESC",
    int Page = 1,
    int PageSize = 20) : IRequest<ApiResponse<PagedResult<DocumentListItemDto>>>;

public record SavedSearchDto(
    int SearchId,
    string NameAr,
    string? NameEn,
    bool IsPublic,
    DateTime? LastRunAt,
    int RunCount);

public record CreateSavedSearchCommand(
    string NameAr,
    string? NameEn,
    string QueryJson,
    bool IsPublic = false) : IRequest<ApiResponse<SavedSearchDto>>;

public record GetSavedSearchesQuery() : IRequest<ApiResponse<List<SavedSearchDto>>>;

// ============================================================
// EVENT HANDLERS
// ============================================================
namespace Darah.ECM.Application.EventHandlers;

using Darah.ECM.Domain.Events;
using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

/// <summary>When a document is approved — send notification and update workspace status.</summary>
public class DocumentApprovedEventHandler : IEventHandler<DocumentApprovedEvent>
{
    private readonly IEmailService _email;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DocumentApprovedEventHandler> _logger;

    public DocumentApprovedEventHandler(IEmailService email, IUnitOfWork uow,
        ILogger<DocumentApprovedEventHandler> logger)
        { _email = email; _uow = uow; _logger = logger; }

    public async Task HandleAsync(DocumentApprovedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Document {DocId} approved by user {UserId}",
            @event.DocumentId, @event.ApprovedBy);

        var doc = await _uow.Documents.GetByGuidAsync(@event.DocumentId, ct);
        if (doc?.CreatedBy is int ownerId)
        {
            var owner = await _uow.Users.GetByIdAsync(ownerId, ct) as Domain.Entities.User;
            if (owner?.Email is string email && !string.IsNullOrEmpty(email))
                await _email.SendTemplatedAsync(email, "DOC_APPROVED",
                    new Dictionary<string, string>
                    {
                        ["DOCUMENT_NUMBER"] = @event.DocumentNumber,
                        ["TITLE"] = @event.DocumentNumber
                    }, ct);
        }
    }
}

/// <summary>When SLA is breached — escalate and notify.</summary>
public class SLABreachedEventHandler : IEventHandler<SLABreachedEvent>
{
    private readonly IEmailService _email;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SLABreachedEventHandler> _logger;

    public SLABreachedEventHandler(IEmailService email, IUnitOfWork uow,
        ILogger<SLABreachedEventHandler> logger)
        { _email = email; _uow = uow; _logger = logger; }

    public async Task HandleAsync(SLABreachedEvent @event, CancellationToken ct = default)
    {
        _logger.LogWarning("SLA breached for task {TaskId}, instance {InstanceId}",
            @event.TaskId, @event.InstanceId);

        if (@event.AssignedToUserId.HasValue)
        {
            var user = await _uow.Users.GetByIdAsync(@event.AssignedToUserId.Value, ct)
                as Domain.Entities.User;
            if (user?.Email is string email)
                await _email.SendTemplatedAsync(email, "SLA_BREACH",
                    new Dictionary<string, string>
                    {
                        ["TASK_ID"] = @event.TaskId.ToString(),
                        ["DUE_AT"] = @event.DueAt.ToString("yyyy-MM-dd HH:mm")
                    }, ct);
        }
    }
}

/// <summary>When a workspace is linked to external — trigger initial sync.</summary>
public class WorkspaceLinkedEventHandler : IEventHandler<WorkspaceLinkedToExternalEvent>
{
    private readonly IMetadataSyncEngine _syncEngine;
    private readonly ILogger<WorkspaceLinkedEventHandler> _logger;

    public WorkspaceLinkedEventHandler(IMetadataSyncEngine syncEngine,
        ILogger<WorkspaceLinkedEventHandler> logger)
        { _syncEngine = syncEngine; _logger = logger; }

    public async Task HandleAsync(WorkspaceLinkedToExternalEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Workspace {WsId} linked to {System}/{ObjId} — triggering initial sync",
            @event.WorkspaceId, @event.ExternalSystemId, @event.ExternalObjectId);
        await _syncEngine.TriggerSyncAsync(@event.WorkspaceId, SyncDirection.Inbound, ct);
    }
}

/// <summary>When workspace archived — cascade to documents.</summary>
public class WorkspaceArchivedEventHandler : IEventHandler<WorkspaceArchivedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ILogger<WorkspaceArchivedEventHandler> _logger;

    public WorkspaceArchivedEventHandler(IUnitOfWork uow, IAuditService audit,
        ILogger<WorkspaceArchivedEventHandler> logger)
        { _uow = uow; _audit = audit; _logger = logger; }

    public async Task HandleAsync(WorkspaceArchivedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Cascading archive from workspace {WsId}", @event.WorkspaceId);
        // In full implementation: load all workspace documents and transition their status to Archived
        await _audit.LogAsync("WorkspaceArchivedCascade", "Workspace",
            @event.WorkspaceId.ToString(), ct: ct);
    }
}

/// <summary>When document retention expires — flag for disposal review.</summary>
public class RetentionExpiredEventHandler : IEventHandler<RetentionExpiredEvent>
{
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly ILogger<RetentionExpiredEventHandler> _logger;

    public RetentionExpiredEventHandler(IAuditService audit, IEmailService email,
        ILogger<RetentionExpiredEventHandler> logger)
        { _audit = audit; _email = email; _logger = logger; }

    public async Task HandleAsync(RetentionExpiredEvent @event, CancellationToken ct = default)
    {
        _logger.LogWarning("Retention expired for document {DocNumber} on {Date}",
            @event.DocumentNumber, @event.ExpiredOn);
        await _audit.LogAsync("RetentionExpired", "Document", @event.DocumentId.ToString(),
            severity: "Warning", additionalInfo: $"ExpiredOn: {@event.ExpiredOn}", ct: ct);
    }
}

// Placeholder interfaces referenced from event handlers
public interface IMetadataSyncEngine
{
    Task TriggerSyncAsync(Guid workspaceId, SyncDirection direction, CancellationToken ct = default);
    Task BulkSyncAsync(string systemCode, CancellationToken ct = default);
}
public enum SyncDirection { Inbound, Outbound, Bidirectional }
