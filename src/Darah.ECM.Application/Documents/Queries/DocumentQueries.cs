using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using MediatR;

namespace Darah.ECM.Application.Documents.Queries;

public sealed record DocumentDto(
    Guid DocumentId, string DocumentNumber, string TitleAr, string? TitleEn,
    string? DocumentTypeNameAr, string? DocumentTypeNameEn, string? LibraryNameAr,
    string? FolderNameAr, string StatusCode, string? StatusDisplayAr,
    string ClassificationCode, string? ClassificationDisplayAr,
    string? CurrentVersionNumber, string? FileExtension, long? FileSizeBytes,
    bool IsCheckedOut, string? CheckedOutByNameAr, bool IsLegalHold,
    DateOnly? RetentionExpiresAt, DateOnly? DocumentDate, string? Keywords, string? Summary,
    DateTime CreatedAt, string? CreatedByNameAr, DateTime? UpdatedAt,
    Guid? PrimaryWorkspaceId, string? WorkspaceTitleAr,
    List<DocumentVersionDto> Versions,
    List<DocumentMetadataDto> MetadataValues,
    List<DocumentRelationDto> Relations,
    List<string> Tags);

public sealed record DocumentVersionDto(
    int VersionId, string VersionNumber, int MajorVersion, int MinorVersion,
    string StorageKey, string OriginalFileName, string FileExtension, long FileSizeBytes,
    string ContentHash, string? ChangeNote, string? CheckInNote, bool IsCurrent,
    DateTime CreatedAt, string? CreatedByNameAr);

public sealed record DocumentMetadataDto(
    int FieldId, string FieldCode, string LabelAr, string LabelEn, string FieldType,
    string? Value, string? DisplayValue, string SourceType);

public sealed record DocumentRelationDto(
    int RelationId, Guid TargetDocumentId, string TargetDocumentNumber,
    string TargetTitleAr, string RelationType, string? Note);

public sealed record DownloadFileDto(
    string StorageKey, string FileName, string ContentType,
    bool RequiresWatermark, string DocumentNumber);

public sealed record VersionComparisonDto(
    DocumentVersionDto PreviousVersion, DocumentVersionDto CurrentVersion,
    bool SameFile, long SizeDeltaBytes, string? SizeDeltaDisplay,
    List<MetadataChangedFieldDto> MetadataChanges);

public sealed record MetadataChangedFieldDto(
    string FieldCode, string LabelAr, string? PreviousValue, string? CurrentValue);

public sealed record PreviewDto(
    string PreviewType, string? ContentUrl, bool RequiresWatermark);

// ── Queries ──────────────────────────────────────────────────────────────────
public sealed record GetDocumentByIdQuery(Guid DocumentId)
    : IRequest<ApiResponse<DocumentDto>>;

public sealed record SearchDocumentsQuery(
    string? TextQuery, int? DocumentTypeId, int? LibraryId, int? FolderId,
    string? StatusCode, int? ClassificationOrder, int? CreatedBy,
    DateTime? DateFrom, DateTime? DateTo, bool? IsLegalHold,
    List<int>? TagIds, Guid? WorkspaceId, string? ExternalSystemId,
    string? ExternalObjectId, Dictionary<int, string>? MetadataFilters,
    string SortBy = "CreatedAt", string SortDirection = "DESC",
    int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<DocumentListItemDto>>>;

public sealed record GetDocumentVersionsQuery(Guid DocumentId)
    : IRequest<ApiResponse<List<DocumentVersionDto>>>;

public sealed record GetDocumentDownloadQuery(Guid DocumentId, int? VersionId)
    : IRequest<ApiResponse<DownloadFileDto>>;

public sealed record CompareDocumentVersionsQuery(Guid DocumentId, int VersionId1, int VersionId2)
    : IRequest<ApiResponse<VersionComparisonDto>>;

public sealed record GetDocumentRelationsQuery(Guid DocumentId)
    : IRequest<ApiResponse<List<DocumentRelationDto>>>;

public sealed record GetDocumentPreviewQuery(Guid DocumentId, int? VersionId)
    : IRequest<ApiResponse<PreviewDto>>;

// ── Handlers ─────────────────────────────────────────────────────────────────
public sealed class GetDocumentByIdQueryHandler
    : IRequestHandler<GetDocumentByIdQuery, ApiResponse<DocumentDto>>
{
    private readonly IDocumentQueryRepository _queryRepo;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    public GetDocumentByIdQueryHandler(IDocumentQueryRepository q, ICurrentUser u, IAuditService a)
        { _queryRepo = q; _user = u; _audit = a; }
    public async Task<ApiResponse<DocumentDto>> Handle(GetDocumentByIdQuery q, CancellationToken ct)
    {
        var dto = await _queryRepo.GetDocumentDtoAsync(q.DocumentId, ct);
        if (dto is null) return ApiResponse<DocumentDto>.Fail("الوثيقة غير موجودة");
        await _audit.LogAsync("DocumentViewed", "Document", q.DocumentId.ToString(), ct: ct);
        return ApiResponse<DocumentDto>.Ok(dto);
    }
}

public sealed class CompareDocumentVersionsQueryHandler
    : IRequestHandler<CompareDocumentVersionsQuery, ApiResponse<VersionComparisonDto>>
{
    private readonly IDocumentVersionRepository _verRepo;
    public CompareDocumentVersionsQueryHandler(IDocumentVersionRepository r) => _verRepo = r;
    public async Task<ApiResponse<VersionComparisonDto>> Handle(
        CompareDocumentVersionsQuery q, CancellationToken ct)
    {
        var v1 = await _verRepo.GetByIdAsync(q.VersionId1, ct) as DocumentVersion;
        var v2 = await _verRepo.GetByIdAsync(q.VersionId2, ct) as DocumentVersion;
        if (v1 is null || v2 is null) return ApiResponse<VersionComparisonDto>.Fail("إحدى النسختين غير موجودة");
        if (v1.DocumentId != v2.DocumentId) return ApiResponse<VersionComparisonDto>.Fail("النسختان لا تنتميان لنفس الوثيقة");
        var (prev, curr) = v1.MajorVersion < v2.MajorVersion ||
            (v1.MajorVersion == v2.MajorVersion && v1.MinorVersion <= v2.MinorVersion)
            ? (v1, v2) : (v2, v1);
        var sameFile = prev.File.ContentHash == curr.File.ContentHash;
        var delta = curr.File.FileSizeBytes - prev.File.FileSizeBytes;
        return ApiResponse<VersionComparisonDto>.Ok(new VersionComparisonDto(
            Map(prev), Map(curr), sameFile, delta, delta >= 0 ? $"+{delta / 1024}KB" : $"{delta / 1024}KB",
            new List<MetadataChangedFieldDto>()));
    }
    private static DocumentVersionDto Map(DocumentVersion v) => new(v.VersionId, v.VersionNumber,
        v.MajorVersion, v.MinorVersion, v.File.StorageKey, v.File.OriginalFileName,
        v.File.FileExtension, v.File.FileSizeBytes, v.File.ContentHash,
        v.ChangeNote, v.CheckInNote, v.IsCurrent, v.CreatedAt, null);
}

// ── Read-model repository ────────────────────────────────────────────────────
public interface IDocumentQueryRepository
{
    Task<DocumentDto?> GetDocumentDtoAsync(Guid documentId, CancellationToken ct = default);
    Task<PagedResult<DocumentListItemDto>> SearchAsync(SearchDocumentsQuery q, CancellationToken ct = default);
    Task<List<DocumentVersionDto>> GetVersionsAsync(Guid documentId, CancellationToken ct = default);
    Task<List<DocumentRelationDto>> GetRelationsAsync(Guid documentId, CancellationToken ct = default);
}
