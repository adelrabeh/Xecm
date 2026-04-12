
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Application.Documents.Commands;

public sealed record CreateDocumentCommand : IRequest<ApiResponse<DocumentCreatedDto>>
{
    public string    TitleAr                  { get; init; } = string.Empty;
    public string?   TitleEn                  { get; init; }
    public int       DocumentTypeId           { get; init; }
    public int       LibraryId                { get; init; }
    public int?      FolderId                 { get; init; }
    public int       ClassificationLevelOrder { get; init; } = 2;
    public DateOnly? DocumentDate             { get; init; }
    public string?   Keywords                 { get; init; }
    public string?   Summary                  { get; init; }
    public int?      RetentionPolicyId        { get; init; }
    public Guid?     WorkspaceId              { get; init; }
    public FileUploadRequest File             { get; init; } = null!;
    public Dictionary<int, string> MetadataValues { get; init; } = new();
    public List<int> TagIds                   { get; init; } = new();
}

public sealed class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    private const long MaxFileSizeBytes = 512L * 1024 * 1024;
    public CreateDocumentCommandValidator()
    {
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(500).WithMessage("عنوان الوثيقة مطلوب ولا يتجاوز 500 حرف");
        RuleFor(x => x.DocumentTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع الوثيقة");
        RuleFor(x => x.LibraryId).GreaterThan(0).WithMessage("يجب اختيار المكتبة");
        RuleFor(x => x.ClassificationLevelOrder).InclusiveBetween(1, 4).WithMessage("مستوى التصنيف غير صحيح");
        RuleFor(x => x.File).NotNull().WithMessage("يجب رفع ملف");
        When(x => x.File != null, () =>
        {
            RuleFor(x => x.File.Length).GreaterThan(0).WithMessage("الملف فارغ")
                .LessThanOrEqualTo(MaxFileSizeBytes).WithMessage("حجم الملف يتجاوز الحد (512 MB)");
            RuleFor(x => x.File.FileName)
                .Must(name => FileMetadata.AllowedExtensions.Contains(Path.GetExtension(name ?? "").ToLowerInvariant()))
                .WithMessage("نوع الملف غير مدعوم");
        });
    }
}

public sealed record DocumentCreatedDto(
    Guid DocumentId, string DocumentNumber, int InitialVersionId, string VersionNumber,
    string TitleAr, string? TitleEn, string ClassificationCode, DateTime CreatedAt);

public sealed class CreateDocumentCommandHandler : IRequestHandler<CreateDocumentCommand, ApiResponse<DocumentCreatedDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileValidationService _fileValidation;
    private readonly IAuditService _audit;
    private readonly IDocumentNumberGenerator _numbering;
    private readonly ILogger<CreateDocumentCommandHandler> _logger;

    public CreateDocumentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser,
        IFileStorageService fileStorage, IFileValidationService fileValidation,
        IAuditService audit, IDocumentNumberGenerator numbering,
        ILogger<CreateDocumentCommandHandler> logger)
    { _uow = uow; _currentUser = currentUser; _fileStorage = fileStorage;
      _fileValidation = fileValidation; _audit = audit; _numbering = numbering; _logger = logger; }

    public async Task<ApiResponse<DocumentCreatedDto>> Handle(CreateDocumentCommand cmd, CancellationToken ct)
    {
        // Step 0: Validate file content (magic bytes)
        cmd.File.Content.Position = 0;
        var validation = await _fileValidation.ValidateAsync(cmd.File.Content, cmd.File.FileName, cmd.File.ContentType, ct);
        if (!validation.IsValid)
            return ApiResponse<DocumentCreatedDto>.Fail($"الملف غير صالح: {validation.FailureReason}");

        string? storedKey = null;
        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Step 1: Store file (outside DB tx — physical storage)
            cmd.File.Content.Position = 0;
            storedKey = await _fileStorage.StoreAsync(cmd.File.Content, cmd.File.FileName, cmd.File.ContentType, ct);
            cmd.File.Content.Position = 0;
            var hash = await ComputeSha256Async(cmd.File.Content, ct);
            var fileMetadata = FileMetadata.Create(storedKey, cmd.File.FileName, cmd.File.ContentType, cmd.File.Length, hash, _fileStorage.ProviderName);

            // Step 2: Generate number + create aggregate
            var docNumber = await _numbering.GenerateAsync(cmd.DocumentTypeId, ct);
            var classification = ClassificationLevel.FromOrder(cmd.ClassificationLevelOrder);
            var document = Document.Create(cmd.TitleAr, cmd.DocumentTypeId, cmd.LibraryId,
                _currentUser.UserId, docNumber, cmd.TitleEn, cmd.FolderId, classification,
                cmd.DocumentDate, cmd.Keywords, cmd.Summary, cmd.RetentionPolicyId);

            if (cmd.WorkspaceId.HasValue)
                document.SetPrimaryWorkspace(cmd.WorkspaceId.Value, _currentUser.UserId);

            await _uow.Documents.AddAsync(document, ct);
            await _uow.CommitAsync(ct); // → DocumentId confirmed

            // Step 3: Create version (CheckOut required)
            document.CheckOut(_currentUser.UserId);
            var version = DocumentVersion.Create(document.DocumentId, "1.0", 1, 0,
                fileMetadata, _currentUser.UserId, "النسخة الأولى", "رفع مباشر");
            await _uow.DocumentVersions.AddAsync(version, ct);
            await _uow.CommitAsync(ct); // → VersionId is real (never 0)

            // Step 4: CheckIn with confirmed VersionId
            document.CheckIn(version.VersionId, _currentUser.UserId); // throws if VersionId <= 0
            await _uow.CommitAsync(ct); // → CurrentVersionId consistent

            await _uow.CommitTransactionAsync(ct); // ATOMIC COMMIT

            _logger.LogInformation("Document created: {Num} DocId={DocId} VersionId={VerId}",
                document.DocumentNumber, document.DocumentId, version.VersionId);

            await _audit.LogAsync("DocumentCreated", "Document", document.DocumentId.ToString(),
                newValues: new { document.DocumentNumber, VersionId = version.VersionId }, ct: ct);

            // Step 5: Dispatch domain events AFTER successful commit
            await _uow.DispatchDomainEventsAsync(ct);

            return ApiResponse<DocumentCreatedDto>.Ok(new DocumentCreatedDto(
                document.DocumentId, document.DocumentNumber, version.VersionId,
                version.VersionNumber, document.TitleAr, document.TitleEn,
                document.Classification.Code, document.CreatedAt), "تم رفع الوثيقة بنجاح");
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            _logger.LogError(ex, "Document creation rolled back. OrphanedKey={Key}", storedKey);
            if (storedKey is not null)
                _logger.LogWarning("Orphaned file pending cleanup: {Key}", storedKey);
            return ApiResponse<DocumentCreatedDto>.Fail("فشل في رفع الوثيقة. يرجى المحاولة مجدداً.");
        }
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken ct)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record CheckOutDocumentCommand(Guid DocumentId) : IRequest<ApiResponse<bool>>;
public sealed class CheckOutDocumentCommandHandler : IRequestHandler<CheckOutDocumentCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _uow; private readonly ICurrentUser _user; private readonly IAuditService _audit;
    public CheckOutDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAuditService audit) { _uow = uow; _user = user; _audit = audit; }
    public async Task<ApiResponse<bool>> Handle(CheckOutDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (doc is null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");
        try { doc.CheckOut(_user.UserId); await _uow.CommitAsync(ct); await _uow.DispatchDomainEventsAsync(ct); await _audit.LogAsync("DocumentCheckedOut", "Document", doc.DocumentId.ToString(), ct: ct); return ApiResponse<bool>.Ok(true, "تم سحب الوثيقة"); }
        catch (InvalidOperationException ex) { return ApiResponse<bool>.Fail(ex.Message); }
    }
}

public sealed record DeleteDocumentCommand(Guid DocumentId, string? Reason) : IRequest<ApiResponse<bool>>;
public sealed class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _uow; private readonly ICurrentUser _user; private readonly IAuditService _audit;
    public DeleteDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAuditService audit) { _uow = uow; _user = user; _audit = audit; }
    public async Task<ApiResponse<bool>> Handle(DeleteDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (doc is null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");
        if (!doc.CanBeDeleted()) return ApiResponse<bool>.Fail("لا يمكن حذف الوثيقة (تجميد قانوني أو حالة نهائية)");
        doc.SoftDelete(_user.UserId);
        await _uow.CommitAsync(ct); await _uow.DispatchDomainEventsAsync(ct);
        await _audit.LogAsync("DocumentDeleted", "Document", doc.DocumentId.ToString(), additionalInfo: cmd.Reason, ct: ct);
        return ApiResponse<bool>.Ok(true, "تم حذف الوثيقة");
    }
}

public sealed record ApplyLegalHoldToDocumentCommand(Guid DocumentId) : IRequest<ApiResponse<bool>>;
public sealed class ApplyLegalHoldToDocumentCommandHandler : IRequestHandler<ApplyLegalHoldToDocumentCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _uow; private readonly ICurrentUser _user; private readonly IAuditService _audit;
    public ApplyLegalHoldToDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAuditService audit) { _uow = uow; _user = user; _audit = audit; }
    public async Task<ApiResponse<bool>> Handle(ApplyLegalHoldToDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (doc is null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");
        doc.ApplyLegalHold();
        await _uow.CommitAsync(ct); await _uow.DispatchDomainEventsAsync(ct);
        await _audit.LogAsync("LegalHoldApplied", "Document", doc.DocumentId.ToString(), ct: ct);
        return ApiResponse<bool>.Ok(true, "تم تطبيق التجميد القانوني");
    }
}
