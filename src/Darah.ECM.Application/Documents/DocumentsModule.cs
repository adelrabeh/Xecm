// ============================================================
// DOCUMENT DTOs
// ============================================================
namespace Darah.ECM.Application.Documents.DTOs;

public record DocumentDto(
    Guid DocumentId,
    string DocumentNumber,
    string TitleAr,
    string? TitleEn,
    string? DocumentTypeNameAr,
    string? LibraryNameAr,
    string? FolderNameAr,
    string StatusCode,
    string? StatusAr,
    string ClassificationCode,
    string? ClassificationAr,
    string? CurrentVersion,
    string? FileExtension,
    long? FileSizeBytes,
    bool IsCheckedOut,
    bool IsLegalHold,
    string? CheckedOutByNameAr,
    DateOnly? RetentionExpiresAt,
    DateOnly? DocumentDate,
    string? Keywords,
    string? Summary,
    DateTime CreatedAt,
    string? CreatedByNameAr,
    DateTime? UpdatedAt,
    Guid? PrimaryWorkspaceId,
    List<DocumentVersionDto> Versions,
    List<MetadataValueDto> MetadataValues,
    List<string> Tags);

public record DocumentVersionDto(
    int VersionId,
    string VersionNumber,
    string FileName,
    string FileExtension,
    long FileSizeBytes,
    string? ChangeNote,
    bool IsCurrent,
    DateTime CreatedAt,
    string? CreatedByNameAr,
    string ContentHash);

public record MetadataValueDto(
    int FieldId,
    string FieldCode,
    string LabelAr,
    string LabelEn,
    string FieldType,
    string? Value,
    string? DisplayValue,
    string? SourceType);

public record DocumentListItemDto(
    Guid DocumentId,
    string DocumentNumber,
    string TitleAr,
    string? TitleEn,
    string? DocumentTypeNameAr,
    string? LibraryNameAr,
    string StatusCode,
    string ClassificationCode,
    string? FileExtension,
    long? FileSizeBytes,
    bool IsCheckedOut,
    bool IsLegalHold,
    DateTime CreatedAt,
    string? CreatedByNameAr);

// ============================================================
// COMMANDS
// ============================================================
namespace Darah.ECM.Application.Documents.Commands;

using MediatR;
using FluentValidation;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Documents.DTOs;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;

// ── Create Document ───────────────────────────────────────────
public record CreateDocumentCommand(
    string TitleAr,
    string? TitleEn,
    int DocumentTypeId,
    int LibraryId,
    int? FolderId,
    int ClassificationLevelOrder,
    DateOnly? DocumentDate,
    string? Keywords,
    string? Summary,
    IFormFile File,
    Dictionary<int, string> MetadataValues,
    List<int> TagIds,
    Guid? WorkspaceId) : IRequest<ApiResponse<DocumentDto>>;

public class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(500)
            .WithMessage("عنوان الوثيقة مطلوب ولا يتجاوز 500 حرف");
        RuleFor(x => x.DocumentTypeId).GreaterThan(0)
            .WithMessage("يجب اختيار نوع الوثيقة");
        RuleFor(x => x.LibraryId).GreaterThan(0)
            .WithMessage("يجب اختيار المكتبة");
        RuleFor(x => x.ClassificationLevelOrder).InclusiveBetween(1, 4)
            .WithMessage("مستوى التصنيف غير صحيح");
        RuleFor(x => x.File).NotNull()
            .WithMessage("يجب رفع ملف");
        RuleFor(x => x.File.Length)
            .LessThanOrEqualTo(536_870_912L).When(x => x.File != null)
            .WithMessage("حجم الملف يتجاوز الحد المسموح (512 MB)");
    }
}

public class CreateDocumentCommandHandler
    : IRequestHandler<CreateDocumentCommand, ApiResponse<DocumentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IFileStorageService _storage;
    private readonly IAuditService _audit;
    private readonly IDocumentNumberGenerator _numbering;

    public CreateDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user,
        IFileStorageService storage, IAuditService audit, IDocumentNumberGenerator numbering)
    {
        _uow = uow; _user = user; _storage = storage; _audit = audit; _numbering = numbering;
    }

    public async Task<ApiResponse<DocumentDto>> Handle(
        CreateDocumentCommand cmd, CancellationToken ct)
    {
        // Validate file extension via ValueObject
        var ext = Path.GetExtension(cmd.File.FileName).ToLowerInvariant();
        if (!FileMetadata.AllowedExtensions.Contains(ext))
            return ApiResponse<DocumentDto>.Fail($"نوع الملف '{ext}' غير مدعوم");

        // Generate document number
        var docNumber = await _numbering.GenerateAsync(cmd.DocumentTypeId, ct);

        // Store file
        using var stream = cmd.File.OpenReadStream();
        var storageKey = await _storage.StoreAsync(stream, cmd.File.FileName, cmd.File.ContentType, ct);

        // Compute hash
        var hash = await ComputeHashAsync(cmd.File);
        var classification = ClassificationLevel.FromOrder(cmd.ClassificationLevelOrder);

        var file = FileMetadata.Create(storageKey, cmd.File.FileName,
            cmd.File.ContentType, cmd.File.Length, hash, _storage.ProviderName);

        // Create document aggregate
        var doc = Document.Create(cmd.TitleAr, cmd.DocumentTypeId, cmd.LibraryId,
            _user.UserId, docNumber, cmd.TitleEn, cmd.FolderId, classification,
            cmd.DocumentDate, cmd.Keywords, cmd.Summary);

        await _uow.Documents.AddAsync(doc, ct);

        // Create initial version (must check-out first)
        doc.CheckOut(_user.UserId);
        var version = DocumentVersion.Create(doc.DocumentId, "1.0", 1, 0, file, _user.UserId, "Initial version");
        doc.CheckIn(0, _user.UserId); // versionId updated post-save

        // Bind to workspace if provided
        if (cmd.WorkspaceId.HasValue)
            doc.SetPrimaryWorkspace(cmd.WorkspaceId.Value, _user.UserId);

        await _uow.CommitAsync(ct);
        await _audit.LogAsync("DocumentCreated", "Document", doc.DocumentId.ToString(),
            newValues: new { doc.DocumentNumber, doc.TitleAr }, ct: ct);

        return ApiResponse<DocumentDto>.Ok(
            MapToDto(doc, version), "تم رفع الوثيقة بنجاح");
    }

    private static async Task<string> ComputeHashAsync(IFormFile file)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = file.OpenReadStream();
        return Convert.ToHexString(await sha.ComputeHashAsync(stream)).ToLower();
    }

    private static DocumentDto MapToDto(Document doc, DocumentVersion version) => new(
        doc.DocumentId, doc.DocumentNumber, doc.TitleAr, doc.TitleEn,
        null, null, null, doc.Status.Value, null,
        doc.Classification.Code, null, version.VersionNumber,
        version.File.FileExtension, version.File.FileSizeBytes,
        doc.IsCheckedOut, doc.IsLegalHold, null,
        doc.RetentionExpiresAt, doc.DocumentDate,
        doc.Keywords, doc.Summary, doc.CreatedAt, null, null,
        doc.PrimaryWorkspaceId,
        new List<DocumentVersionDto>(), new List<MetadataValueDto>(), new List<string>());
}

// ── Check Out ──────────────────────────────────────────────────
public record CheckOutDocumentCommand(Guid DocumentId) : IRequest<ApiResponse<bool>>;

public class CheckOutDocumentCommandHandler
    : IRequestHandler<CheckOutDocumentCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _uow;
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

// ── Delete Document ─────────────────────────────────────────────
public record DeleteDocumentCommand(Guid DocumentId, string? Reason) : IRequest<ApiResponse<bool>>;

public class DeleteDocumentCommandHandler
    : IRequestHandler<DeleteDocumentCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public DeleteDocumentCommandHandler(IUnitOfWork uow, ICurrentUser user, IAuditService audit)
        { _uow = uow; _user = user; _audit = audit; }

    public async Task<ApiResponse<bool>> Handle(DeleteDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await _uow.Documents.GetByGuidAsync(cmd.DocumentId, ct);
        if (doc is null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");
        if (doc.IsLegalHold) return ApiResponse<bool>.Fail("لا يمكن حذف وثيقة خاضعة لتجميد قانوني");

        doc.SoftDelete(_user.UserId);
        await _uow.CommitAsync(ct);
        await _audit.LogAsync("DocumentDeleted", "Document", doc.DocumentId.ToString(),
            additionalInfo: cmd.Reason, ct: ct);
        return ApiResponse<bool>.Ok(true, "تم حذف الوثيقة");
    }
}

// ============================================================
// QUERIES
// ============================================================
namespace Darah.ECM.Application.Documents.Queries;

using MediatR;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Application.Documents.DTOs;

public record GetDocumentByIdQuery(Guid DocumentId) : IRequest<ApiResponse<DocumentDto>>;

public record SearchDocumentsQuery(
    string? TextQuery,
    int? DocumentTypeId,
    int? LibraryId,
    int? FolderId,
    int? StatusValueId,
    int? ClassificationOrder,
    int? CreatedBy,
    DateTime? DateFrom,
    DateTime? DateTo,
    bool? IsLegalHold,
    List<int>? TagIds,
    Dictionary<int, string>? MetadataFilters,
    Guid? WorkspaceId,
    string SortBy = "CreatedAt",
    string SortDirection = "DESC",
    int Page = 1,
    int PageSize = 20) : IRequest<ApiResponse<PagedResult<DocumentListItemDto>>>;

public record GetDocumentVersionsQuery(Guid DocumentId) : IRequest<ApiResponse<List<DocumentVersionDto>>>;

public record GetDocumentDownloadQuery(Guid DocumentId, int? VersionId)
    : IRequest<ApiResponse<DownloadFileDto>>;

public record DownloadFileDto(string StorageKey, string FileName, string ContentType, bool RequiresWatermark);
