using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Application.Records.Commands;

// ─── DECLARE RECORD ───────────────────────────────────────────────────────────
/// <summary>
/// Formally declares a document as a record, applying record class and retention policy.
/// After declaration: document status → Active, retention expiry computed.
/// </summary>
public sealed record DeclareRecordCommand(
    Guid DocumentId,
    int  RecordClassId,
    int  RetentionPolicyId,
    string? Note = null)
    : IRequest<ApiResponse<RecordDeclarationDto>>;

public sealed class DeclareRecordCommandValidator : AbstractValidator<DeclareRecordCommand>
{
    public DeclareRecordCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.RecordClassId).GreaterThan(0)
            .WithMessage("يجب تحديد فئة السجل");
        RuleFor(x => x.RetentionPolicyId).GreaterThan(0)
            .WithMessage("يجب تحديد سياسة الاحتفاظ");
    }
}

public sealed class DeclareRecordCommandHandler
    : IRequestHandler<DeclareRecordCommand, ApiResponse<RecordDeclarationDto>>
{
    private readonly IUnitOfWork     _uow;
    private readonly ICurrentUser    _user;
    private readonly IAuditService   _audit;
    private readonly IRecordsRepository _recordsRepo;
    private readonly ILogger<DeclareRecordCommandHandler> _logger;

    public DeclareRecordCommandHandler(IUnitOfWork uow, ICurrentUser user,
        IAuditService audit, IRecordsRepository recordsRepo,
        ILogger<DeclareRecordCommandHandler> logger)
    { _uow = uow; _user = user; _audit = audit; _recordsRepo = recordsRepo; _logger = logger; }

    public async Task<ApiResponse<RecordDeclarationDto>> Handle(
        DeclareRecordCommand cmd, CancellationToken ct)
    {
        var document = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (document is null) return ApiResponse<RecordDeclarationDto>.Fail("الوثيقة غير موجودة");
        if (document.RecordClassId.HasValue)
            return ApiResponse<RecordDeclarationDto>.Fail("الوثيقة مصنفة كسجل بالفعل");

        var retentionPolicy = await _recordsRepo.GetRetentionPolicyAsync(cmd.RetentionPolicyId, ct);
        if (retentionPolicy is null)
            return ApiResponse<RecordDeclarationDto>.Fail("سياسة الاحتفاظ غير موجودة");

        // Apply record declaration
        document.AssignRecordClass(cmd.RecordClassId, _user.UserId);
        var triggerDate = document.DocumentDate ?? DateOnly.FromDateTime(document.CreatedAt);
        var expiryDate  = retentionPolicy.ComputeExpiry(triggerDate);
        document.SetRetentionExpiry(expiryDate, _user.UserId);

        // Transition to Active if still Draft
        if (document.Status == DocumentStatus.Draft)
            document.TransitionStatus(DocumentStatus.Active, _user.UserId);

        await _uow.CommitAsync(ct);
        await _uow.DispatchDomainEventsAsync(ct);

        await _audit.LogAsync("RecordDeclared", "Document", document.DocumentId.ToString(),
            newValues: new
            {
                RecordClassId    = cmd.RecordClassId,
                RetentionPolicy  = retentionPolicy.NameAr,
                ExpiryDate       = expiryDate.ToString()
            }, ct: ct);

        _logger.LogInformation(
            "Record declared: DocId={DocId} RecordClass={RC} Expiry={Expiry}",
            cmd.DocumentId, cmd.RecordClassId, expiryDate);

        return ApiResponse<RecordDeclarationDto>.Ok(new RecordDeclarationDto(
            document.DocumentId, document.DocumentNumber,
            cmd.RecordClassId, retentionPolicy.NameAr,
            expiryDate, retentionPolicy.DisposalAction),
            "تم تصنيف الوثيقة كسجل رسمي بنجاح");
    }
}

// ─── APPLY LEGAL HOLD ─────────────────────────────────────────────────────────
public sealed record ApplyLegalHoldCommand(
    int    HoldId,
    Guid[] DocumentIds,
    string? Note = null)
    : IRequest<ApiResponse<LegalHoldResultDto>>;

public sealed class ApplyLegalHoldCommandValidator : AbstractValidator<ApplyLegalHoldCommand>
{
    public ApplyLegalHoldCommandValidator()
    {
        RuleFor(x => x.HoldId).GreaterThan(0).WithMessage("يجب تحديد أمر التجميد");
        RuleFor(x => x.DocumentIds).NotEmpty()
            .WithMessage("يجب تحديد وثيقة واحدة على الأقل");
        RuleFor(x => x.DocumentIds.Length).LessThanOrEqualTo(1000)
            .WithMessage("لا يمكن تطبيق التجميد على أكثر من 1000 وثيقة في طلب واحد");
    }
}

public sealed class ApplyLegalHoldCommandHandler
    : IRequestHandler<ApplyLegalHoldCommand, ApiResponse<LegalHoldResultDto>>
{
    private readonly IUnitOfWork     _uow;
    private readonly ICurrentUser    _user;
    private readonly IAuditService   _audit;
    private readonly IRecordsRepository _recordsRepo;

    public ApplyLegalHoldCommandHandler(IUnitOfWork uow, ICurrentUser user,
        IAuditService audit, IRecordsRepository recordsRepo)
        { _uow = uow; _user = user; _audit = audit; _recordsRepo = recordsRepo; }

    public async Task<ApiResponse<LegalHoldResultDto>> Handle(
        ApplyLegalHoldCommand cmd, CancellationToken ct)
    {
        var hold = await _recordsRepo.GetLegalHoldAsync(cmd.HoldId, ct);
        if (hold is null || !hold.IsActive)
            return ApiResponse<LegalHoldResultDto>.Fail("أمر التجميد غير موجود أو غير نشط");

        int applied = 0, skipped = 0;
        foreach (var docId in cmd.DocumentIds)
        {
            var doc = await _uow.Documents.GetByGuidAsync(docId, ct);
            if (doc is null) { skipped++; continue; }
            if (doc.IsLegalHold) { skipped++; continue; }

            doc.ApplyLegalHold();
            await _recordsRepo.AddDocumentLegalHoldAsync(new DocumentLegalHold
            {
                DocumentId = docId,
                HoldId     = cmd.HoldId,
                AppliedAt  = DateTime.UtcNow,
                AppliedBy  = _user.UserId
            }, ct);
            applied++;
        }

        await _uow.CommitAsync(ct);
        await _uow.DispatchDomainEventsAsync(ct);

        await _audit.LogAsync("LegalHoldApplied", "LegalHold", cmd.HoldId.ToString(),
            newValues: new { Applied = applied, Skipped = skipped, HoldId = cmd.HoldId }, ct: ct);

        return ApiResponse<LegalHoldResultDto>.Ok(
            new LegalHoldResultDto(cmd.HoldId, hold.HoldCode, applied, skipped),
            $"تم تطبيق التجميد القانوني على {applied} وثيقة");
    }
}

// ─── CREATE DISPOSAL REQUEST ──────────────────────────────────────────────────
public sealed record CreateDisposalRequestCommand(
    string   DisposalType,
    Guid[]   DocumentIds,
    string   Justification)
    : IRequest<ApiResponse<DisposalRequestDto>>;

public sealed class CreateDisposalRequestCommandValidator
    : AbstractValidator<CreateDisposalRequestCommand>
{
    public CreateDisposalRequestCommandValidator()
    {
        RuleFor(x => x.DisposalType)
            .Must(t => new[] { "Delete", "Archive", "Transfer" }.Contains(t))
            .WithMessage("نوع الإتلاف غير صحيح (Delete|Archive|Transfer)");
        RuleFor(x => x.DocumentIds).NotEmpty()
            .WithMessage("يجب تحديد وثائق للإتلاف");
        RuleFor(x => x.Justification).NotEmpty().MinimumLength(20)
            .WithMessage("يجب إدخال مبرر كافٍ للإتلاف (20 حرفاً على الأقل)");
    }
}

public sealed class CreateDisposalRequestCommandHandler
    : IRequestHandler<CreateDisposalRequestCommand, ApiResponse<DisposalRequestDto>>
{
    private readonly IUnitOfWork     _uow;
    private readonly ICurrentUser    _user;
    private readonly IAuditService   _audit;
    private readonly IRecordsRepository _recordsRepo;

    public CreateDisposalRequestCommandHandler(IUnitOfWork uow, ICurrentUser user,
        IAuditService audit, IRecordsRepository recordsRepo)
        { _uow = uow; _user = user; _audit = audit; _recordsRepo = recordsRepo; }

    public async Task<ApiResponse<DisposalRequestDto>> Handle(
        CreateDisposalRequestCommand cmd, CancellationToken ct)
    {
        // Validate: no documents on legal hold can be disposed
        var onHold = new List<string>();
        foreach (var docId in cmd.DocumentIds)
        {
            var doc = await _uow.Documents.GetByGuidAsync(docId, ct);
            if (doc?.IsLegalHold == true) onHold.Add(doc.DocumentNumber);
        }

        if (onHold.Any())
            return ApiResponse<DisposalRequestDto>.Fail(
                $"الوثائق التالية خاضعة لتجميد قانوني ولا يمكن إتلافها: {string.Join(", ", onHold)}");

        var code = $"DISP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 25);
        var request = DisposalRequest.Create(code, cmd.DisposalType,
            cmd.Justification, cmd.DocumentIds.Length, _user.UserId);

        await _recordsRepo.AddDisposalRequestAsync(request, ct);
        await _recordsRepo.AddDisposalDocumentsAsync(request.RequestId, cmd.DocumentIds, ct);
        await _uow.CommitAsync(ct);

        await _audit.LogAsync("DisposalRequestCreated", "DisposalRequest",
            request.RequestId.ToString(),
            newValues: new { code, cmd.DisposalType, DocumentCount = cmd.DocumentIds.Length }, ct: ct);

        return ApiResponse<DisposalRequestDto>.Ok(
            new DisposalRequestDto(request.RequestId, code, cmd.DisposalType,
                "Pending", cmd.DocumentIds.Length, DateTime.UtcNow),
            "تم إنشاء طلب الإتلاف بنجاح. في انتظار الموافقة");
    }
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────
namespace Darah.ECM.Application.Records.DTOs;

public sealed record RecordDeclarationDto(
    Guid    DocumentId,
    string  DocumentNumber,
    int     RecordClassId,
    string  RetentionPolicyName,
    DateOnly RetentionExpiryDate,
    string  DisposalAction);

public sealed record LegalHoldResultDto(
    int    HoldId,
    string HoldCode,
    int    DocumentsApplied,
    int    DocumentsSkipped);

public sealed record DisposalRequestDto(
    int      RequestId,
    string   RequestCode,
    string   DisposalType,
    string   Status,
    int      DocumentCount,
    DateTime CreatedAt);

public sealed record RetentionSummaryDto(
    int TotalActive,
    int TotalExpired,
    int ExpiringIn30Days,
    int OnLegalHold,
    int AwaitingDisposal);
