
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Application.Documents.Commands;

// ─── CHECK IN NEW VERSION ─────────────────────────────────────────────────────
/// <summary>
/// Upload a new version to an already checked-out document.
/// Uses the same single-transaction pattern as document creation.
/// </summary>
public sealed record CheckInNewVersionCommand : IRequest<ApiResponse<NewVersionDto>>
{
    public Guid    DocumentId  { get; init; }
    public string? ChangeNote  { get; init; }
    public string? CheckInNote { get; init; }
    public bool    MajorBump  { get; init; } = false;  // true = 2.0, false = 1.1
    public FileUploadRequest File { get; init; } = null!;
}

public sealed class CheckInNewVersionCommandValidator : AbstractValidator<CheckInNewVersionCommand>
{
    public CheckInNewVersionCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.File).NotNull().WithMessage("يجب رفع ملف");
        When(x => x.File != null, () =>
            RuleFor(x => x.File.Length).GreaterThan(0).WithMessage("الملف فارغ"));
    }
}

public sealed record NewVersionDto(
    Guid   DocumentId,
    int    VersionId,
    string VersionNumber,
    long   FileSizeBytes,
    string ContentHash,
    DateTime CreatedAt);

public sealed class CheckInNewVersionCommandHandler
    : IRequestHandler<CheckInNewVersionCommand, ApiResponse<NewVersionDto>>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUser        _user;
    private readonly IFileStorageService _storage;
    private readonly IFileValidationService _validation;
    private readonly IAuditService       _audit;
    private readonly ILogger<CheckInNewVersionCommandHandler> _logger;

    public CheckInNewVersionCommandHandler(IUnitOfWork uow, ICurrentUser user,
        IFileStorageService storage, IFileValidationService validation,
        IAuditService audit, ILogger<CheckInNewVersionCommandHandler> logger)
    { _uow = uow; _user = user; _storage = storage; _validation = validation;
      _audit = audit; _logger = logger; }

    public async Task<ApiResponse<NewVersionDto>> Handle(
        CheckInNewVersionCommand cmd, CancellationToken ct)
    {
        var document = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (document is null) return ApiResponse<NewVersionDto>.Fail("الوثيقة غير موجودة");
        if (!document.IsCheckedOut)
            return ApiResponse<NewVersionDto>.Fail("الوثيقة لم يتم سحبها. يجب سحبها أولاً");
        if (document.CheckedOutBy != _user.UserId)
            return ApiResponse<NewVersionDto>.Fail("الوثيقة محجوزة من مستخدم آخر");

        // Validate file
        cmd.File.Content.Position = 0;
        var validation = await _validation.ValidateAsync(
            cmd.File.Content, cmd.File.FileName, cmd.File.ContentType, ct);
        if (!validation.IsValid)
            return ApiResponse<NewVersionDto>.Fail($"الملف غير صالح: {validation.FailureReason}");

        string? storedKey = null;
        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Store file
            cmd.File.Content.Position = 0;
            storedKey = await _storage.StoreAsync(
                cmd.File.Content, cmd.File.FileName, cmd.File.ContentType, ct);
            cmd.File.Content.Position = 0;
            var hash = await ComputeHashAsync(cmd.File.Content, ct);

            var fileMetadata = FileMetadata.Create(storedKey, cmd.File.FileName,
                cmd.File.ContentType, cmd.File.Length, hash, _storage.ProviderName);

            // Determine version numbers
            var allVersions = await _uow.DocumentVersions.GetAllForDocumentAsync(cmd.DocumentId, ct);
            var latestMajor = allVersions.Any() ? allVersions.Max(v => v.MajorVersion) : 1;
            var latestMinor = allVersions.Where(v => v.MajorVersion == latestMajor)
                                         .Max(v => (int?)v.MinorVersion) ?? 0;

            int newMajor, newMinor;
            if (cmd.MajorBump) { newMajor = latestMajor + 1; newMinor = 0; }
            else               { newMajor = latestMajor;     newMinor = latestMinor + 1; }

            // Mark current as superseded
            foreach (var v in allVersions.Where(v => v.IsCurrent))
                v.MarkSuperseded();

            var newVersion = DocumentVersion.Create(
                cmd.DocumentId, $"{newMajor}.{newMinor}", newMajor, newMinor,
                fileMetadata, _user.UserId, cmd.ChangeNote, cmd.CheckInNote);

            await _uow.DocumentVersions.AddAsync(newVersion, ct);
            await _uow.CommitAsync(ct); // VersionId confirmed

            document.CheckIn(newVersion.VersionId, _user.UserId);
            await _uow.CommitAsync(ct);

            await _uow.CommitTransactionAsync(ct);
            await _uow.DispatchDomainEventsAsync(ct);

            await _audit.LogAsync("DocumentCheckedIn", "Document", document.DocumentId.ToString(),
                newValues: new { VersionId = newVersion.VersionId, newVersion.VersionNumber }, ct: ct);

            _logger.LogInformation("New version checked in: DocId={DocId} Version={Ver}",
                cmd.DocumentId, newVersion.VersionNumber);

            return ApiResponse<NewVersionDto>.Ok(new NewVersionDto(
                cmd.DocumentId, newVersion.VersionId, newVersion.VersionNumber,
                newVersion.File.FileSizeBytes, newVersion.File.ContentHash, newVersion.CreatedAt),
                "تم إيداع النسخة الجديدة بنجاح");
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            _logger.LogError(ex, "CheckIn failed, rolled back. OrphanedKey={Key}", storedKey);
            return ApiResponse<NewVersionDto>.Fail("فشل في إيداع الوثيقة. يرجى المحاولة مجدداً.");
        }
    }

    private static async Task<string> ComputeHashAsync(Stream s, CancellationToken ct)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(await sha.ComputeHashAsync(s, ct)).ToLowerInvariant();
    }
}

// ─── ADD DOCUMENT RELATION ────────────────────────────────────────────────────
public sealed record AddDocumentRelationCommand(
    Guid   SourceDocumentId,
    Guid   TargetDocumentId,
    string RelationType,
    string? Note)
    : IRequest<ApiResponse<bool>>;

public sealed class AddDocumentRelationCommandValidator
    : AbstractValidator<AddDocumentRelationCommand>
{
    private static readonly string[] ValidTypes =
        { "ParentChild", "Reference", "Supersedes", "RelatedTo", "Attachment" };

    public AddDocumentRelationCommandValidator()
    {
        RuleFor(x => x.SourceDocumentId).NotEmpty();
        RuleFor(x => x.TargetDocumentId).NotEmpty()
            .NotEqual(x => x.SourceDocumentId)
            .WithMessage("لا يمكن ربط الوثيقة بنفسها");
        RuleFor(x => x.RelationType).Must(t => ValidTypes.Contains(t))
            .WithMessage($"نوع العلاقة غير صحيح. المقبول: {string.Join(", ", ValidTypes)}");
    }
}

public sealed class AddDocumentRelationCommandHandler
    : IRequestHandler<AddDocumentRelationCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork  _uow;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IDocumentRelationRepository _relationRepo;

    public AddDocumentRelationCommandHandler(IUnitOfWork uow, ICurrentUser user,
        IAuditService audit, IDocumentRelationRepository relationRepo)
        { _uow = uow; _user = user; _audit = audit; _relationRepo = relationRepo; }

    public async Task<ApiResponse<bool>> Handle(
        AddDocumentRelationCommand cmd, CancellationToken ct)
    {
        var source = await _uow.Documents.GetByGuidAsync(cmd.SourceDocumentId, ct);
        var target = await _uow.Documents.GetByGuidAsync(cmd.TargetDocumentId, ct);
        if (source is null || target is null)
            return ApiResponse<bool>.Fail("إحدى الوثيقتين غير موجودة");

        var exists = await _relationRepo.ExistsAsync(
            cmd.SourceDocumentId, cmd.TargetDocumentId, cmd.RelationType, ct);
        if (exists)
            return ApiResponse<bool>.Fail("هذه العلاقة موجودة بالفعل");

        var relation = DocumentRelation.Create(cmd.SourceDocumentId, cmd.TargetDocumentId,
            cmd.RelationType, _user.UserId, cmd.Note);
        await _relationRepo.AddAsync(relation, ct);
        await _uow.CommitAsync(ct);

        await _audit.LogAsync("DocumentRelationAdded", "DocumentRelation",
            relation.RelationId.ToString(),
            newValues: new { cmd.SourceDocumentId, cmd.TargetDocumentId, cmd.RelationType }, ct: ct);

        return ApiResponse<bool>.Ok(true, "تم إضافة العلاقة بين الوثيقتين");
    }
}

// ─── DOCUMENT RELATION REPOSITORY INTERFACE ───────────────────────────────────
public interface IDocumentRelationRepository
{
    Task<bool>     ExistsAsync(Guid sourceId, Guid targetId, string type, CancellationToken ct);
    Task           AddAsync(DocumentRelation relation, CancellationToken ct);
    Task<List<DocumentRelation>> GetByDocumentAsync(Guid documentId, CancellationToken ct);
    Task           RemoveAsync(int relationId, CancellationToken ct);
}
