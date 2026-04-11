using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.xECM.Application.DTOs;
using Darah.ECM.xECM.Domain.Entities;
using Darah.ECM.xECM.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace Darah.ECM.xECM.Application.Commands;

// ── CreateWorkspace ──────────────────────────────────────────────────────────
public sealed record CreateWorkspaceCommand(
    string TitleAr, string? TitleEn, int WorkspaceTypeId, int OwnerId,
    int? DepartmentId, string? Description, int ClassificationLevelId = 2,
    int? RetentionPolicyId = null, string? ExternalSystemId = null,
    string? ExternalObjectId = null, string? ExternalObjectType = null,
    string? ExternalObjectUrl = null) : IRequest<ApiResponse<WorkspaceDto>>;

public sealed class CreateWorkspaceValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceValidator()
    {
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(500).WithMessage("عنوان مساحة العمل مطلوب");
        RuleFor(x => x.WorkspaceTypeId).GreaterThan(0).WithMessage("يجب تحديد نوع مساحة العمل");
        RuleFor(x => x.OwnerId).GreaterThan(0).WithMessage("يجب تحديد مالك مساحة العمل");
        RuleFor(x => x.ClassificationLevelId).InclusiveBetween(1, 4);
        When(x => !string.IsNullOrEmpty(x.ExternalSystemId), () =>
        {
            RuleFor(x => x.ExternalObjectId).NotEmpty().WithMessage("معرف الكيان الخارجي مطلوب");
            RuleFor(x => x.ExternalObjectType).NotEmpty().WithMessage("نوع الكيان الخارجي مطلوب");
        });
    }
}

public sealed class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, ApiResponse<WorkspaceDto>>
{
    private readonly IWorkspaceRepository _repo;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IWorkspaceNumberGenerator _numbering;
    private readonly IMetadataSyncEngine _syncEngine;

    public CreateWorkspaceCommandHandler(IWorkspaceRepository repo, ICurrentUser user,
        IAuditService audit, IWorkspaceNumberGenerator numbering, IMetadataSyncEngine syncEngine)
    { _repo = repo; _user = user; _audit = audit; _numbering = numbering; _syncEngine = syncEngine; }

    public async Task<ApiResponse<WorkspaceDto>> Handle(CreateWorkspaceCommand cmd, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(cmd.ExternalSystemId) && !string.IsNullOrEmpty(cmd.ExternalObjectId))
        {
            var alreadyBound = await _repo.ExternalBindingExistsAsync(cmd.ExternalSystemId, cmd.ExternalObjectId!, ct);
            if (alreadyBound) return ApiResponse<WorkspaceDto>.Fail("يوجد مساحة عمل مرتبطة بهذا الكيان الخارجي بالفعل");
        }

        var number = await _numbering.GenerateAsync(cmd.WorkspaceTypeId, ct);
        var ws = Workspace.Create(cmd.TitleAr, cmd.WorkspaceTypeId, cmd.OwnerId, 1 /* active status */,
            number, _user.UserId, cmd.TitleEn, cmd.DepartmentId, cmd.Description,
            cmd.ClassificationLevelId, cmd.RetentionPolicyId);

        if (!string.IsNullOrEmpty(cmd.ExternalSystemId))
            ws.BindToExternal(cmd.ExternalSystemId, cmd.ExternalObjectId!, cmd.ExternalObjectType!, cmd.ExternalObjectUrl, _user.UserId);

        await _repo.AddAsync(ws, ct);
        await _audit.LogAsync("WorkspaceCreated", "Workspace", ws.WorkspaceId.ToString(), newValues: new { ws.WorkspaceNumber, ws.TitleAr }, ct: ct);

        if (ws.IsBoundToExternal)
            await _syncEngine.TriggerSyncAsync(ws.WorkspaceId, SyncDirection.Inbound, ct);

        return ApiResponse<WorkspaceDto>.Ok(MapToDto(ws), "تم إنشاء مساحة العمل بنجاح");
    }

    private static WorkspaceDto MapToDto(Workspace ws) => new(
        ws.WorkspaceId, ws.WorkspaceNumber, ws.TitleAr, ws.TitleEn,
        null, null, null, null, null, null,
        ws.IsBoundToExternal, ws.ExternalSystemId, ws.ExternalObjectId,
        ws.ExternalObjectType, ws.ExternalObjectUrl, ws.SyncStatus, ws.LastSyncedAt,
        ws.IsLegalHold, ws.RetentionExpiresAt, 0, ws.CreatedAt, ws.UpdatedAt);
}

// ── BindExternalObject ───────────────────────────────────────────────────────
public sealed record BindExternalObjectCommand(Guid WorkspaceId, string ExternalSystemId,
    string ExternalObjectId, string ExternalObjectType, string? ExternalObjectUrl,
    bool TriggerImmediateSync = true) : IRequest<ApiResponse<bool>>;

public sealed class BindExternalObjectCommandHandler : IRequestHandler<BindExternalObjectCommand, ApiResponse<bool>>
{
    private readonly IWorkspaceRepository _repo;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IMetadataSyncEngine _sync;

    public BindExternalObjectCommandHandler(IWorkspaceRepository repo, ICurrentUser user, IAuditService audit, IMetadataSyncEngine sync)
    { _repo = repo; _user = user; _audit = audit; _sync = sync; }

    public async Task<ApiResponse<bool>> Handle(BindExternalObjectCommand cmd, CancellationToken ct)
    {
        var ws = await _repo.GetByGuidAsync(cmd.WorkspaceId, ct);
        if (ws is null) return ApiResponse<bool>.Fail("مساحة العمل غير موجودة");

        try { ws.BindToExternal(cmd.ExternalSystemId, cmd.ExternalObjectId, cmd.ExternalObjectType, cmd.ExternalObjectUrl, _user.UserId); }
        catch (InvalidOperationException ex) { return ApiResponse<bool>.Fail(ex.Message); }

        await _audit.LogAsync("WorkspaceLinkedToExternal", "Workspace", ws.WorkspaceId.ToString(), newValues: new { cmd.ExternalSystemId, cmd.ExternalObjectId }, ct: ct);

        if (cmd.TriggerImmediateSync)
            await _sync.TriggerSyncAsync(cmd.WorkspaceId, SyncDirection.Inbound, ct);

        return ApiResponse<bool>.Ok(true, "تم ربط مساحة العمل بالكيان الخارجي بنجاح");
    }
}

// ── Archive ──────────────────────────────────────────────────────────────────
public sealed record ArchiveWorkspaceCommand(Guid WorkspaceId, string? Reason) : IRequest<ApiResponse<bool>>;
public sealed class ArchiveWorkspaceCommandHandler : IRequestHandler<ArchiveWorkspaceCommand, ApiResponse<bool>>
{
    private readonly IWorkspaceRepository _repo; private readonly ICurrentUser _user; private readonly IAuditService _audit;
    public ArchiveWorkspaceCommandHandler(IWorkspaceRepository r, ICurrentUser u, IAuditService a) { _repo = r; _user = u; _audit = a; }
    public async Task<ApiResponse<bool>> Handle(ArchiveWorkspaceCommand cmd, CancellationToken ct)
    {
        var ws = await _repo.GetByGuidAsync(cmd.WorkspaceId, ct);
        if (ws is null) return ApiResponse<bool>.Fail("مساحة العمل غير موجودة");
        ws.Archive(_user.UserId);
        await _audit.LogAsync("WorkspaceArchived", "Workspace", ws.WorkspaceId.ToString(), additionalInfo: cmd.Reason, ct: ct);
        return ApiResponse<bool>.Ok(true, "تمت أرشفة مساحة العمل");
    }
}

// ── Legal Hold ───────────────────────────────────────────────────────────────
public sealed record ApplyWorkspaceLegalHoldCommand(Guid WorkspaceId) : IRequest<ApiResponse<bool>>;
public sealed class ApplyWorkspaceLegalHoldCommandHandler : IRequestHandler<ApplyWorkspaceLegalHoldCommand, ApiResponse<bool>>
{
    private readonly IWorkspaceRepository _repo; private readonly ICurrentUser _user; private readonly IAuditService _audit;
    public ApplyWorkspaceLegalHoldCommandHandler(IWorkspaceRepository r, ICurrentUser u, IAuditService a) { _repo = r; _user = u; _audit = a; }
    public async Task<ApiResponse<bool>> Handle(ApplyWorkspaceLegalHoldCommand cmd, CancellationToken ct)
    {
        var ws = await _repo.GetByGuidAsync(cmd.WorkspaceId, ct);
        if (ws is null) return ApiResponse<bool>.Fail("مساحة العمل غير موجودة");
        ws.ApplyLegalHold(_user.UserId);
        await _audit.LogAsync("WorkspaceLegalHoldApplied", "Workspace", ws.WorkspaceId.ToString(), ct: ct);
        return ApiResponse<bool>.Ok(true, "تم تطبيق التجميد القانوني على مساحة العمل وجميع وثائقها");
    }
}

// ── Trigger Sync ─────────────────────────────────────────────────────────────
public sealed record TriggerWorkspaceSyncCommand(Guid WorkspaceId, string Direction = "Inbound") : IRequest<ApiResponse<SyncResultDto>>;
public sealed class TriggerWorkspaceSyncCommandHandler : IRequestHandler<TriggerWorkspaceSyncCommand, ApiResponse<SyncResultDto>>
{
    private readonly IMetadataSyncEngine _sync;
    public TriggerWorkspaceSyncCommandHandler(IMetadataSyncEngine sync) => _sync = sync;
    public async Task<ApiResponse<SyncResultDto>> Handle(TriggerWorkspaceSyncCommand cmd, CancellationToken ct)
    {
        if (!Enum.TryParse<SyncDirection>(cmd.Direction, true, out var dir))
            return ApiResponse<SyncResultDto>.Fail("اتجاه المزامنة غير صحيح. القيم المقبولة: Inbound, Outbound, Bidirectional");
        var result = await _sync.TriggerSyncAsync(cmd.WorkspaceId, dir, ct);
        return ApiResponse<SyncResultDto>.Ok(new SyncResultDto(result.IsSuccess, result.FieldsUpdated, result.ConflictsDetected, result.ErrorMessage, result.DurationMs));
    }
}

// ── Resolve Conflict ─────────────────────────────────────────────────────────
public sealed record ResolveWorkspaceConflictCommand(Guid WorkspaceId, int FieldId, string Resolution) : IRequest<ApiResponse<bool>>;
public sealed class ResolveWorkspaceConflictCommandHandler : IRequestHandler<ResolveWorkspaceConflictCommand, ApiResponse<bool>>
{
    private readonly IMetadataSyncEngine _sync;
    public ResolveWorkspaceConflictCommandHandler(IMetadataSyncEngine sync) => _sync = sync;
    public async Task<ApiResponse<bool>> Handle(ResolveWorkspaceConflictCommand cmd, CancellationToken ct)
    {
        var ok = await _sync.ResolveConflictAsync(cmd.WorkspaceId, cmd.FieldId, cmd.Resolution, ct);
        return ok ? ApiResponse<bool>.Ok(true, "تم حل التعارض") : ApiResponse<bool>.Fail("لم يتم العثور على التعارض المطلوب");
    }
}
