using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Services;
using MediatR;

namespace Darah.ECM.Application.Notifications;

// ─── NOTIFICATION SERVICE ─────────────────────────────────────────────────────
/// <summary>
/// Application-level notification service.
/// Handles in-app + email notifications for workflow, document, and system events.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(int userId, string title, string body, string type,
        string? entityType = null, string? entityId = null,
        string? actionUrl = null, CancellationToken ct = default);

    Task NotifyWorkflowTaskAssignedAsync(int assignedUserId, string documentTitle,
        string workflowName, string stepName, int taskId, CancellationToken ct = default);

    Task NotifySLABreachAsync(int assignedUserId, string documentTitle,
        string workflowName, DateTime dueAt, CancellationToken ct = default);

    Task NotifyDocumentApprovedAsync(int documentOwnerUserId, string documentTitle,
        string documentNumber, CancellationToken ct = default);

    Task NotifyDocumentRejectedAsync(int documentOwnerUserId, string documentTitle,
        string documentNumber, string? reason, CancellationToken ct = default);

    Task NotifyRetentionExpiringAsync(int ownerUserId, string documentNumber,
        DateOnly expiryDate, CancellationToken ct = default);
}

// ─── MARK NOTIFICATION READ ───────────────────────────────────────────────────
public sealed record MarkNotificationReadCommand(long NotificationId)
    : IRequest<ApiResponse<bool>>;

public sealed record MarkAllNotificationsReadCommand()
    : IRequest<ApiResponse<bool>>;

public sealed record GetNotificationsQuery(
    bool? UnreadOnly = null,
    int   Page       = 1,
    int   PageSize   = 20)
    : IRequest<ApiResponse<PagedResult<NotificationDto>>>;

public sealed record GetUnreadCountQuery()
    : IRequest<ApiResponse<int>>;

public sealed record NotificationDto(
    long     NotificationId,
    string   Title,
    string   Body,
    string   NotificationType,
    string?  EntityType,
    string?  EntityId,
    string?  ActionUrl,
    int      Priority,
    bool     IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

// ─── METADATA ENGINE COMMANDS ─────────────────────────────────────────────────
namespace Darah.ECM.Application.Metadata.Commands;

public sealed record CreateMetadataFieldCommand(
    string   FieldCode,
    string   LabelAr,
    string   LabelEn,
    string   FieldType,
    bool     IsRequired    = false,
    bool     IsSearchable  = true,
    int?     LookupCategoryId = null,
    string?  ValidationRegex = null,
    string?  HelpTextAr    = null,
    string?  HelpTextEn    = null,
    int?     MaxLength      = null,
    int      SortOrder      = 0)
    : IRequest<ApiResponse<MetadataFieldDto>>;

public sealed record UpdateDocumentMetadataCommand(
    Guid DocumentId,
    Dictionary<int, string?> FieldValues)   // FieldId → raw value (null = clear)
    : IRequest<ApiResponse<bool>>;

public sealed class UpdateDocumentMetadataCommandHandler
    : IRequestHandler<UpdateDocumentMetadataCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork    _uow;
    private readonly ICurrentUser   _user;
    private readonly IAuditService  _audit;
    private readonly IMetadataRepository _metaRepo;

    public UpdateDocumentMetadataCommandHandler(IUnitOfWork uow, ICurrentUser user,
        IAuditService audit, IMetadataRepository metaRepo)
        { _uow = uow; _user = user; _audit = audit; _metaRepo = metaRepo; }

    public async Task<ApiResponse<bool>> Handle(
        UpdateDocumentMetadataCommand cmd, CancellationToken ct)
    {
        var document = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (document is null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");
        if (document.IsLegalHold)
            return ApiResponse<bool>.Fail("لا يمكن تعديل بيانات وثيقة خاضعة لتجميد قانوني");

        var fields = await _metaRepo.GetFieldsForDocumentTypeAsync(
            document.DocumentTypeId, ct);

        var errors = new List<string>();
        foreach (var (fieldId, rawValue) in cmd.FieldValues)
        {
            var field = fields.FirstOrDefault(f => f.FieldId == fieldId);
            if (field is null) continue;

            var (isValid, error) = field.Validate(rawValue);
            if (!isValid) errors.Add(error!);
        }

        if (errors.Any())
            return ApiResponse<bool>.ValidationFail(errors);

        // Persist validated values
        foreach (var (fieldId, rawValue) in cmd.FieldValues)
        {
            var field = fields.FirstOrDefault(f => f.FieldId == fieldId);
            if (field is null) continue;

            var existing = await _metaRepo.GetValueAsync(cmd.DocumentId, fieldId, ct);
            if (existing is null)
            {
                var newVal = new DocumentMetadataValue
                    { DocumentId = cmd.DocumentId, FieldId = fieldId };
                newVal.SetValue(field.FieldType, rawValue);
                await _metaRepo.AddValueAsync(newVal, ct);
            }
            else
            {
                existing.SetValue(field.FieldType, rawValue);
                _metaRepo.UpdateValue(existing);
            }
        }

        await _uow.CommitAsync(ct);
        document.SetUpdated(_user.UserId);

        await _audit.LogAsync("MetadataUpdated", "Document", document.DocumentId.ToString(),
            newValues: cmd.FieldValues, ct: ct);

        return ApiResponse<bool>.Ok(true, "تم حفظ البيانات الوصفية بنجاح");
    }
}

namespace Darah.ECM.Application.Metadata.DTOs;

public sealed record MetadataFieldDto(
    int      FieldId,
    string   FieldCode,
    string   LabelAr,
    string   LabelEn,
    string   FieldType,
    bool     IsRequired,
    bool     IsSearchable,
    bool     IsMultiValue,
    int?     MaxLength,
    string?  ValidationRegex,
    string?  HelpTextAr,
    string?  HelpTextEn,
    int      SortOrder,
    bool     IsActive,
    List<LookupValueDto>? Options = null);

public sealed record LookupValueDto(int ValueId, string Code, string ValueAr, string ValueEn);

public sealed record MetadataFormDto(
    int                     DocumentTypeId,
    string                  DocumentTypeNameAr,
    List<MetadataGroupDto>  Groups);

public sealed record MetadataGroupDto(
    string                  GroupName,
    List<MetadataFieldWithValueDto> Fields);

public sealed record MetadataFieldWithValueDto(
    MetadataFieldDto Field,
    string?          CurrentValue,
    string?          DisplayValue);

// Interfaces referenced from handlers
public interface IMetadataRepository
{
    Task<List<MetadataField>> GetFieldsForDocumentTypeAsync(int documentTypeId, CancellationToken ct = default);
    Task<DocumentMetadataValue?> GetValueAsync(Guid documentId, int fieldId, CancellationToken ct = default);
    Task<List<DocumentMetadataValue>> GetAllValuesAsync(Guid documentId, CancellationToken ct = default);
    Task AddValueAsync(DocumentMetadataValue value, CancellationToken ct = default);
    void UpdateValue(DocumentMetadataValue value);
}

public interface IRecordsRepository
{
    Task<RetentionPolicy?> GetRetentionPolicyAsync(int policyId, CancellationToken ct = default);
    Task<LegalHold?> GetLegalHoldAsync(int holdId, CancellationToken ct = default);
    Task AddDocumentLegalHoldAsync(DocumentLegalHold link, CancellationToken ct = default);
    Task AddDisposalRequestAsync(DisposalRequest request, CancellationToken ct = default);
    Task AddDisposalDocumentsAsync(int requestId, Guid[] documentIds, CancellationToken ct = default);
    Task<List<Document>> GetExpiringDocumentsAsync(int daysAhead, CancellationToken ct = default);
}

public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(int folderId, CancellationToken ct = default);
    Task AddAsync(Folder folder, CancellationToken ct = default);
    Task<List<Folder>> GetChildrenAsync(int? parentId, int libraryId, CancellationToken ct = default);
    Task<List<Folder>> GetByPathPrefixAsync(string pathPrefix, CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

public interface IWorkflowEngine
{
    Task<int> StartAsync(Guid documentId, int definitionId, int startedBy, int priority, CancellationToken ct);
    Task<bool> ProcessActionAsync(int taskId, string action, int actionBy, string? comment, int? delegateToUserId, CancellationToken ct);
    Task<int?> DetectWorkflowDefinitionAsync(int documentTypeId, CancellationToken ct);
    Task CheckSLABreachesAsync(CancellationToken ct);
}
