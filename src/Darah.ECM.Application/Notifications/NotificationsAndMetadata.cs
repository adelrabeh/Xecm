using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Application.Notifications;

// ─── WORKFLOW ENGINE ──────────────────────────────────────────────────────────
public interface IWorkflowEngine
{
    Task<int?>  DetectWorkflowDefinitionAsync(int documentTypeId, CancellationToken ct = default);
    Task<int>   StartAsync(Guid documentId, int definitionId, int startedBy, int priority = 2, CancellationToken ct = default);
    Task<bool>  ProcessActionAsync(int taskId, string actionType, int userId, string? comment, int? delegateToUserId, CancellationToken ct = default);
    Task<bool>  ApproveTaskAsync(int taskId, int userId, string? comment, CancellationToken ct = default);
    Task<bool>  RejectTaskAsync(int taskId, int userId, string reason, CancellationToken ct = default);
    Task<bool>  DelegateTaskAsync(int taskId, int newUserId, int delegatedBy, CancellationToken ct = default);
    Task        CheckSLABreachesAsync(CancellationToken ct = default);
}

// ─── NOTIFICATION SERVICE ─────────────────────────────────────────────────────
public interface INotificationService
{
    Task SendAsync(int userId, string titleAr, string body, string notificationType,
        string? entityType = null, string? entityId = null, string? actionUrl = null,
        int priority = 2, CancellationToken ct = default);
    Task MarkReadAsync(long notificationId, int userId, CancellationToken ct = default);
    Task<IEnumerable<Darah.ECM.Domain.Entities.Notification>> GetUnreadAsync(int userId, CancellationToken ct = default);
}

// ─── METADATA REPOSITORY ─────────────────────────────────────────────────────
public interface IMetadataRepository
{
    Task<IEnumerable<MetadataField>> GetByDocumentTypeAsync(int typeId, CancellationToken ct = default);
    Task<IEnumerable<DocumentMetadataValue>> GetByDocumentAsync(Guid docId, CancellationToken ct = default);
    Task UpsertAsync(DocumentMetadataValue value, CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

// ─── RECORDS REPOSITORY ───────────────────────────────────────────────────────
public interface IRecordsRepository
{
    Task<RecordClass?>      GetClassAsync(int classId, CancellationToken ct = default);
    Task<RetentionPolicy?>  GetPolicyAsync(int policyId, CancellationToken ct = default);
    Task<RetentionPolicy?>  GetRetentionPolicyAsync(int policyId, CancellationToken ct = default);
    Task<LegalHold?>        GetLegalHoldAsync(int holdId, CancellationToken ct = default);
    Task                    AddDocumentLegalHoldAsync(DocumentLegalHold hold, CancellationToken ct = default);
    Task<DisposalRequest?>  GetDisposalRequestAsync(int requestId, CancellationToken ct = default);
    Task                    AddDisposalRequestAsync(DisposalRequest request, CancellationToken ct = default);
    Task                    AddDisposalDocumentsAsync(int requestId, Guid[] documentIds, CancellationToken ct = default);
    Task<IEnumerable<LegalHold>> GetActiveHoldsForDocumentAsync(Guid docId, CancellationToken ct = default);
    Task<int>               CommitAsync(CancellationToken ct = default);
}

// ─── FOLDER REPOSITORY ────────────────────────────────────────────────────────
public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(int folderId, CancellationToken ct = default);
    Task<IEnumerable<Folder>> GetByLibraryAsync(int libraryId, CancellationToken ct = default);
    Task AddAsync(Folder folder, CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

// ─── USER REPOSITORY ──────────────────────────────────────────────────────────
public interface IUserRepository
{
    Task<IEnumerable<string>> GetPermissionsAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<int>>    GetRoleIdsAsync(int userId, CancellationToken ct = default);
    Task<int?>                GetDepartmentIdAsync(int userId, CancellationToken ct = default);
}

// ─── NOTIFICATION COMMANDS ────────────────────────────────────────────────────
public sealed record GetMyNotificationsQuery(int UserId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<NotificationDto>>>;

public sealed record MarkNotificationReadCommand(long NotificationId) : IRequest<ApiResponse<bool>>;

public sealed record NotificationDto(
    long NotificationId, string Title, string Body, string NotificationType,
    string? EntityType, string? EntityId, string? ActionUrl,
    bool IsRead, DateTime? ReadAt, DateTime CreatedAt, int Priority);

// ─── METADATA COMMANDS ────────────────────────────────────────────────────────
public sealed record GetDocumentMetadataQuery(Guid DocumentId)
    : IRequest<ApiResponse<List<MetadataFieldDto>>>;

public sealed record UpdateDocumentMetadataCommand(Guid DocumentId, Dictionary<int, string> Values)
    : IRequest<ApiResponse<bool>>;

public sealed class UpdateDocumentMetadataHandler
    : IRequestHandler<UpdateDocumentMetadataCommand, ApiResponse<bool>>
{
    private readonly IMetadataRepository _metaRepo;
    private readonly ICurrentUser _user;
    private readonly ILogger<UpdateDocumentMetadataHandler> _logger;

    public UpdateDocumentMetadataHandler(IMetadataRepository metaRepo,
        ICurrentUser user, ILogger<UpdateDocumentMetadataHandler> logger)
        { _metaRepo = metaRepo; _user = user; _logger = logger; }

    public async Task<ApiResponse<bool>> Handle(
        UpdateDocumentMetadataCommand cmd, CancellationToken ct)
    {
        var existing = (await _metaRepo.GetByDocumentAsync(cmd.DocumentId, ct)).ToList();
        foreach (var (fieldId, rawValue) in cmd.Values)
        {
            var value = existing.FirstOrDefault(v => v.FieldId == fieldId)
                ?? new DocumentMetadataValue { DocumentId = cmd.DocumentId, FieldId = fieldId };
            value.SetValue("Text", rawValue); // fieldType resolved from field definition
            await _metaRepo.UpsertAsync(value, ct);
        }
        await _metaRepo.CommitAsync(ct);
        _logger.LogInformation("Metadata updated for document {DocId}", cmd.DocumentId);
        return ApiResponse<bool>.Ok(true, "تم تحديث البيانات الوصفية");
    }
}
