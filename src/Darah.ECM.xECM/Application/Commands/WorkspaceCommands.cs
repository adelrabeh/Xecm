using Darah.ECM.Application.Common.Correlation;
using Darah.ECM.Application.Common.Guards;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.xECM.Domain.Entities;
using Darah.ECM.xECM.Domain.Interfaces;
using Darah.ECM.xECM.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.xECM.Application.Commands;

// DTOs
public sealed record WorkspaceDto(
    Guid WorkspaceId, string WorkspaceNumber, string TitleAr, string? TitleEn,
    int WorkspaceTypeId, string? WorkspaceTypeNameAr, string? WorkspaceTypeCode,
    string StatusCode, string ClassificationCode, string? ClassificationAr,
    int OwnerId, string? OwnerNameAr, int? DepartmentId, string? DepartmentAr,
    string? Description, int Priority, int DocumentCount,
    bool IsLegalHold, DateTime? LegalHoldAt, int? RetentionPolicyId,
    DateOnly? RetentionExpiresAt, int? DefaultWorkflowId,
    bool IsBoundToExternal, string? ExternalSystemCode, string? ExternalObjectId,
    string? ExternalObjectType, string? ExternalObjectUrl, string? ExternalObjectTitle,
    string? SyncStatus, DateTime? LastSyncedAt, string? SyncError,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record WorkspaceListItemDto(
    Guid WorkspaceId, string WorkspaceNumber, string TitleAr, string? TitleEn,
    string? WorkspaceTypeCode, string? WorkspaceTypeNameAr, string StatusCode,
    string ClassificationCode, string? OwnerNameAr, int DocumentCount,
    bool IsLegalHold, bool IsBoundToExternal, string? ExternalSystemCode,
    string? SyncStatus, DateTime CreatedAt);

public sealed record WorkspaceDocumentDto(
    int BindingId, Guid WorkspaceId, Guid DocumentId, string BindingType,
    string DocumentNumber, string DocumentTitleAr, string StatusCode,
    string ClassificationCode, string? FileExtension, bool IsLegalHold,
    DateTime BoundAt, string? BoundByNameAr, string? Note);

public sealed record LegalHoldResultDto(Guid WorkspaceId, string WorkspaceNumber, int DocumentsAffected);

// Commands
public sealed record CreateWorkspaceCommand(
    string TitleAr, string? TitleEn, int WorkspaceTypeId, int OwnerId,
    int? DepartmentId, string? Description, int ClassificationLevelOrder = 2,
    int? RetentionPolicyId = null, int? DefaultWorkflowId = null, int Priority = 2,
    string? ExternalSystemCode = null, string? ExternalObjectId = null,
    string? ExternalObjectType = null, string? ExternalObjectUrl = null,
    Dictionary<int, string>? MetadataValues = null)
    : IRequest<ApiResponse<WorkspaceDto>>;

public sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(500).WithMessage("عنوان مساحة العمل مطلوب");
        RuleFor(x => x.WorkspaceTypeId).GreaterThan(0).WithMessage("يجب تحديد نوع مساحة العمل");
        RuleFor(x => x.OwnerId).GreaterThan(0).WithMessage("يجب تحديد المالك");
        RuleFor(x => x.ClassificationLevelOrder).InclusiveBetween(1, 4);
        When(x => !string.IsNullOrEmpty(x.ExternalSystemCode), () => {
            RuleFor(x => x.ExternalObjectId).NotEmpty().WithMessage("معرف الكيان الخارجي مطلوب");
            RuleFor(x => x.ExternalObjectType).NotEmpty().WithMessage("نوع الكيان الخارجي مطلوب");
        });
    }
}

public sealed class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, ApiResponse<WorkspaceDto>>
{
    private readonly IWorkspaceRepository _wsRepo;
    private readonly IWorkspaceNumberGenerator _numbering;
    private readonly ICurrentUser _user;
    private readonly IStructuredAuditService _audit;
    private readonly IAuthorizationGuard _authGuard;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CreateWorkspaceCommandHandler> _logger;

    public CreateWorkspaceCommandHandler(IWorkspaceRepository wsRepo, IWorkspaceNumberGenerator numbering,
        ICurrentUser user, IStructuredAuditService audit, IAuthorizationGuard authGuard,
        IEventBus eventBus, ILogger<CreateWorkspaceCommandHandler> logger)
    { _wsRepo = wsRepo; _numbering = numbering; _user = user; _audit = audit;
      _authGuard = authGuard; _eventBus = eventBus; _logger = logger; }

    public async Task<ApiResponse<WorkspaceDto>> Handle(CreateWorkspaceCommand cmd, CancellationToken ct)
    {
        var auth = await _authGuard.AuthorizeAdminActionAsync("workspace.create", ct);
        if (!auth.IsGranted) return auth.ToFailResponse<WorkspaceDto>();

        if (!string.IsNullOrEmpty(cmd.ExternalSystemCode) && !string.IsNullOrEmpty(cmd.ExternalObjectId))
        {
            var exists = await _wsRepo.ExternalBindingExistsAsync(cmd.ExternalSystemCode, cmd.ExternalObjectId, ct);
            if (exists) return ApiResponse<WorkspaceDto>.Fail("يوجد مساحة عمل مرتبطة بهذا الكيان الخارجي بالفعل");
        }

        var number = await _numbering.GenerateAsync(cmd.WorkspaceTypeId, "", ct);
        var classification = ClassificationLevel.FromOrder(cmd.ClassificationLevelOrder);

        var ws = Workspace.Create(cmd.TitleAr, cmd.WorkspaceTypeId, cmd.OwnerId, number,
            _user.UserId, cmd.TitleEn, cmd.DepartmentId, cmd.Description,
            classification, cmd.RetentionPolicyId, cmd.DefaultWorkflowId, cmd.Priority);

        if (!string.IsNullOrEmpty(cmd.ExternalSystemCode))
            ws.BindToExternalObject(cmd.ExternalSystemCode, cmd.ExternalObjectId!,
                cmd.ExternalObjectType!, cmd.ExternalObjectUrl, null, _user.UserId);

        await _wsRepo.AddAsync(ws, ct);
        await _wsRepo.CommitAsync(ct);

        foreach (var ev in ws.DomainEvents) await _eventBus.PublishAsync(ev, ct);
        ws.ClearDomainEvents();

        await _audit.LogSuccessAsync("WorkspaceCreated", AuditEntry.Modules.System,
            "Workspace", ws.WorkspaceId.ToString(),
            newValues: new { ws.WorkspaceNumber, ws.TitleAr }, ct: ct);

        _logger.LogInformation("Workspace created: {Num} {Title}", ws.WorkspaceNumber, ws.TitleAr);
        return ApiResponse<WorkspaceDto>.Ok(Map(ws), "تم إنشاء مساحة العمل بنجاح");
    }

    private static WorkspaceDto Map(Workspace ws) => new(ws.WorkspaceId, ws.WorkspaceNumber, ws.TitleAr, ws.TitleEn,
        ws.WorkspaceTypeId, null, null, ws.Status.Value, ws.Classification.Code, ws.Classification.NameAr,
        ws.OwnerId, null, ws.DepartmentId, null, ws.Description, ws.Priority, ws.DocumentCount,
        ws.IsLegalHold, ws.LegalHoldAt, ws.RetentionPolicyId, ws.RetentionExpiresAt, ws.DefaultWorkflowId,
        ws.IsBoundToExternal, ws.ExternalSystemCode, ws.ExternalObjectId, ws.ExternalObjectType,
        ws.ExternalObjectUrl, ws.ExternalObjectTitle, ws.SyncStatus, ws.LastSyncedAt, ws.SyncError,
        ws.CreatedAt, ws.UpdatedAt);
}

// Lifecycle commands
public sealed record UpdateWorkspaceCommand(Guid WorkspaceId, string TitleAr, string? TitleEn, string? Description,
    int ClassificationLevelOrder, int? RetentionPolicyId, int? DefaultWorkflowId) : IRequest<ApiResponse<WorkspaceDto>>;
public sealed record ActivateWorkspaceCommand(Guid WorkspaceId) : IRequest<ApiResponse<bool>>;
public sealed record CloseWorkspaceCommand(Guid WorkspaceId, string? Reason) : IRequest<ApiResponse<bool>>;
public sealed record ArchiveWorkspaceCommand(Guid WorkspaceId) : IRequest<ApiResponse<bool>>;
public sealed record DisposeWorkspaceCommand(Guid WorkspaceId) : IRequest<ApiResponse<bool>>;
public sealed record ApplyWorkspaceLegalHoldCommand(Guid WorkspaceId) : IRequest<ApiResponse<LegalHoldResultDto>>;
public sealed record ReleaseWorkspaceLegalHoldCommand(Guid WorkspaceId) : IRequest<ApiResponse<bool>>;
public sealed record BindDocumentToWorkspaceCommand(Guid WorkspaceId, Guid DocumentId, string BindingType = "Primary", string? Note = null) : IRequest<ApiResponse<WorkspaceDocumentDto>>;
public sealed record RemoveDocumentFromWorkspaceCommand(Guid WorkspaceId, Guid DocumentId) : IRequest<ApiResponse<bool>>;
public sealed record BindExternalObjectCommand(Guid WorkspaceId, string ExternalSystemCode, string ExternalObjectId, string ExternalObjectType, string? ExternalObjectUrl, string? ExternalObjectTitle, bool TriggerImmediateSync = true) : IRequest<ApiResponse<bool>>;
public sealed record UnbindExternalObjectCommand(Guid WorkspaceId) : IRequest<ApiResponse<bool>>;
public sealed record TriggerWorkspaceSyncCommand(Guid WorkspaceId, string Direction = "Inbound") : IRequest<ApiResponse<SyncResultDto>>;
public sealed record ResolveConflictCommand(Guid WorkspaceId, int FieldId, string Resolution) : IRequest<ApiResponse<bool>>;

public sealed record SyncResultDto(bool IsSuccess, int FieldsUpdated, int ConflictsDetected, string? ErrorMessage, long DurationMs);

public sealed class BindExternalObjectValidator : AbstractValidator<BindExternalObjectCommand>
{
    public BindExternalObjectValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.ExternalSystemCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ExternalObjectId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExternalObjectType).NotEmpty().MaximumLength(100);
    }
}
