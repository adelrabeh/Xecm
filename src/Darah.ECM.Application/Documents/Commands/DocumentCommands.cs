using Darah.ECM.Application.Common.Abstractions;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Darah.ECM.Application.Documents.Commands;

// ─── COMMAND ──────────────────────────────────────────────────────────────────
/// <summary>
/// Command to create a new document with an initial version.
/// Uses <see cref="FileUploadRequest"/> instead of IFormFile to respect
/// Clean Architecture: Application layer has NO dependency on ASP.NET Core.
/// </summary>
public sealed record CreateDocumentCommand : IRequest<ApiResponse<DocumentCreatedDto>>
{
    public string    TitleAr                 { get; init; } = string.Empty;
    public string?   TitleEn                 { get; init; }
    public int       DocumentTypeId          { get; init; }
    public int       LibraryId               { get; init; }
    public int?      FolderId                { get; init; }
    public int       ClassificationLevelOrder { get; init; } = 2;  // default: Internal
    public DateOnly? DocumentDate            { get; init; }
    public string?   Keywords                { get; init; }
    public string?   Summary                 { get; init; }
    public int?      RetentionPolicyId       { get; init; }
    public Guid?     WorkspaceId             { get; init; }
    public FileUploadRequest File             { get; init; } = null!;
    public Dictionary<int, string> MetadataValues { get; init; } = new();
    public List<int> TagIds                  { get; init; } = new();
}

// ─── VALIDATOR ────────────────────────────────────────────────────────────────
public sealed class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    private const long MaxFileSizeBytes = 512L * 1024 * 1024; // 512 MB

    public CreateDocumentCommandValidator()
    {
        RuleFor(x => x.TitleAr)
            .NotEmpty().MaximumLength(500)
            .WithMessage("عنوان الوثيقة مطلوب ولا يتجاوز 500 حرف");

        RuleFor(x => x.DocumentTypeId)
            .GreaterThan(0)
            .WithMessage("يجب اختيار نوع الوثيقة");

        RuleFor(x => x.LibraryId)
            .GreaterThan(0)
            .WithMessage("يجب اختيار المكتبة");

        RuleFor(x => x.ClassificationLevelOrder)
            .InclusiveBetween(1, 4)
            .WithMessage("مستوى التصنيف غير صحيح. القيم المقبولة: 1 (عام) إلى 4 (سري للغاية)");

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("يجب رفع ملف");

        RuleFor(x => x.File.Length)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .When(x => x.File != null)
            .WithMessage("حجم الملف يتجاوز الحد المسموح (512 MB)");

        RuleFor(x => x.File.FileName)
            .Must(name => FileMetadata.AllowedExtensions
                .Contains(Path.GetExtension(name).ToLowerInvariant()))
            .When(x => x.File != null)
            .WithMessage("نوع الملف غير مدعوم");
    }
}

// ─── RESPONSE DTO ─────────────────────────────────────────────────────────────
public sealed record DocumentCreatedDto(
    Guid   DocumentId,
    string DocumentNumber,
    int    InitialVersionId,
    string VersionNumber,
    string TitleAr,
    string? TitleEn,
    DateTime CreatedAt);

// ─── HANDLER ─────────────────────────────────────────────────────────────────
/// <summary>
/// Orchestrates document creation with correct version persistence.
///
/// CRITICAL FIX — Version persistence flow:
///   1. Store file physically → get storage key + hash
///   2. Create document aggregate (status=Draft)
///   3. Create DocumentVersion record with real FileMetadata
///   4. Persist version first (so EF generates VersionId)
///   5. Call document.CheckIn(persistedVersionId) — never with placeholder 0
///   6. Commit all changes atomically
///
/// This guarantees CurrentVersionId is always a real, resolvable FK.
/// </summary>
public sealed class CreateDocumentCommandHandler
    : IRequestHandler<CreateDocumentCommand, ApiResponse<DocumentCreatedDto>>
{
    private readonly IUnitOfWork              _uow;
    private readonly ICurrentUser             _currentUser;
    private readonly IFileStorageService      _fileStorage;
    private readonly IAuditService            _audit;
    private readonly IDocumentNumberGenerator _numbering;

    public CreateDocumentCommandHandler(
        IUnitOfWork              uow,
        ICurrentUser             currentUser,
        IFileStorageService      fileStorage,
        IAuditService            audit,
        IDocumentNumberGenerator numbering)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _audit       = audit;
        _numbering   = numbering;
    }

    public async Task<ApiResponse<DocumentCreatedDto>> Handle(
        CreateDocumentCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            // ── Step 1: Store file (outside DB transaction — if this fails, no DB changes) ──
            var storageKey = await _fileStorage.StoreAsync(
                cmd.File.Content, cmd.File.FileName, cmd.File.ContentType, ct);

            var hash = await ComputeSha256Async(cmd.File.Content, ct);

            var fileMetadata = FileMetadata.Create(
                storageKey,
                cmd.File.FileName,
                cmd.File.ContentType,
                cmd.File.Length,
                hash,
                _fileStorage.ProviderName);

            // ── Step 2: Generate unique document number ────────────────────────
            var docNumber = await _numbering.GenerateAsync(cmd.DocumentTypeId, ct);

            // ── Step 3: Create document aggregate ─────────────────────────────
            var classification = ClassificationLevel.FromOrder(cmd.ClassificationLevelOrder);

            var document = Document.Create(
                titleAr:           cmd.TitleAr,
                documentTypeId:    cmd.DocumentTypeId,
                libraryId:         cmd.LibraryId,
                createdBy:         _currentUser.UserId,
                documentNumber:    docNumber,
                titleEn:           cmd.TitleEn,
                folderId:          cmd.FolderId,
                classification:    classification,
                documentDate:      cmd.DocumentDate,
                keywords:          cmd.Keywords,
                summary:           cmd.Summary,
                retentionPolicyId: cmd.RetentionPolicyId);

            // Bind to workspace if provided
            if (cmd.WorkspaceId.HasValue)
                document.SetPrimaryWorkspace(cmd.WorkspaceId.Value, _currentUser.UserId);

            await _uow.Documents.AddAsync(document, ct);
            await _uow.CommitAsync(ct);  // document gets its DocumentId confirmed

            // ── Step 4: Check-out to allow version creation ────────────────────
            document.CheckOut(_currentUser.UserId);

            // ── Step 5: Create initial version ────────────────────────────────
            var version = DocumentVersion.Create(
                documentId:   document.DocumentId,
                versionNumber: "1.0",
                major:        1,
                minor:        0,
                file:         fileMetadata,
                createdBy:    _currentUser.UserId,
                changeNote:   "النسخة الأولى",
                checkInNote:  "رفع مباشر");

            await _uow.DocumentVersions.AddAsync(version, ct);
            await _uow.CommitAsync(ct);  // ← version.VersionId is NOW populated by EF/DB

            // ── Step 6: Check-in with the real persisted VersionId ─────────────
            // This is the critical fix: VersionId is guaranteed non-zero here.
            document.CheckIn(version.VersionId, _currentUser.UserId);
            await _uow.CommitAsync(ct);  // persist CurrentVersionId on Document

            // ── Step 7: Metadata values ────────────────────────────────────────
            // (Persisted via DocumentMetadataValueRepository — omitted for brevity,
            //  follows same AddAsync → CommitAsync pattern)

            await _uow.CommitTransactionAsync(ct);

            await _audit.LogAsync(
                eventType:  "DocumentCreated",
                entityType: "Document",
                entityId:   document.DocumentId.ToString(),
                newValues:  new { document.DocumentNumber, document.TitleAr, VersionId = version.VersionId },
                ct: ct);

            return ApiResponse<DocumentCreatedDto>.Ok(new DocumentCreatedDto(
                document.DocumentId,
                document.DocumentNumber,
                version.VersionId,
                version.VersionNumber,
                document.TitleAr,
                document.TitleEn,
                document.CreatedAt),
                "تم رفع الوثيقة بنجاح");
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);

            // Attempt to clean up the stored file if DB transaction failed
            // In production, use a compensating background job for safety
            throw;
        }
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken ct)
    {
        stream.Position = 0;
        using var sha   = System.Security.Cryptography.SHA256.Create();
        var hash        = await sha.ComputeHashAsync(stream, ct);
        stream.Position = 0;  // reset for storage use
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

// ─── OTHER DOCUMENT COMMANDS ─────────────────────────────────────────────────
public sealed record CheckOutDocumentCommand(Guid DocumentId) : IRequest<ApiResponse<bool>>;

public sealed class CheckOutDocumentCommandHandler
    : IRequestHandler<CheckOutDocumentCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork  _uow;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public CheckOutDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAuditService audit)
        { _uow = uow; _user = user; _audit = audit; }

    public async Task<ApiResponse<bool>> Handle(CheckOutDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (doc is null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");
        try
        {
            doc.CheckOut(_user.UserId);
            await _uow.CommitAsync(ct);
            await _audit.LogAsync("DocumentCheckedOut", "Document", doc.DocumentId.ToString(), ct: ct);
            return ApiResponse<bool>.Ok(true, "تم سحب الوثيقة بنجاح");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Message);
        }
    }
}

public sealed record DeleteDocumentCommand(Guid DocumentId, string? Reason) : IRequest<ApiResponse<bool>>;

public sealed class DeleteDocumentCommandHandler
    : IRequestHandler<DeleteDocumentCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork  _uow;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public DeleteDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAuditService audit)
        { _uow = uow; _user = user; _audit = audit; }

    public async Task<ApiResponse<bool>> Handle(DeleteDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (doc is null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");
        if (!doc.CanBeDeleted()) return ApiResponse<bool>.Fail("لا يمكن حذف هذه الوثيقة (تجميد قانوني أو حالة نهائية)");

        doc.SoftDelete(_user.UserId);
        await _uow.CommitAsync(ct);
        await _audit.LogAsync("DocumentDeleted", "Document", doc.DocumentId.ToString(),
            additionalInfo: cmd.Reason, ct: ct);
        return ApiResponse<bool>.Ok(true, "تم حذف الوثيقة");
    }
}

public sealed record ApplyLegalHoldToDocumentCommand(Guid DocumentId) : IRequest<ApiResponse<bool>>;

public sealed class ApplyLegalHoldToDocumentCommandHandler
    : IRequestHandler<ApplyLegalHoldToDocumentCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork  _uow;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public ApplyLegalHoldToDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAuditService audit)
        { _uow = uow; _user = user; _audit = audit; }

    public async Task<ApiResponse<bool>> Handle(ApplyLegalHoldToDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (doc is null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");

        doc.ApplyLegalHold();
        await _uow.CommitAsync(ct);
        await _audit.LogAsync("LegalHoldApplied", "Document", doc.DocumentId.ToString(), ct: ct);
        return ApiResponse<bool>.Ok(true, "تم تطبيق التجميد القانوني");
    }
}
