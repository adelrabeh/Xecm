using Darah.ECM.Application.Common.Models;
using MediatR;

namespace Darah.ECM.Application.Documents.DTOs;

public sealed record DocumentDto(
    Guid     DocumentId,
    string   DocumentNumber,
    string   TitleAr,
    string?  TitleEn,
    string?  DocumentTypeNameAr,
    string?  DocumentTypeNameEn,
    string?  LibraryNameAr,
    string?  FolderNameAr,
    string   StatusCode,
    string?  StatusDisplayAr,
    string   ClassificationCode,
    string?  ClassificationDisplayAr,
    string?  CurrentVersionNumber,
    string?  FileExtension,
    long?    FileSizeBytes,
    bool     IsCheckedOut,
    string?  CheckedOutByNameAr,
    bool     IsLegalHold,
    DateOnly? RetentionExpiresAt,
    DateOnly? DocumentDate,
    string?  Keywords,
    string?  Summary,
    DateTime CreatedAt,
    string?  CreatedByNameAr,
    DateTime? UpdatedAt,
    Guid?    PrimaryWorkspaceId,
    string?  WorkspaceTitleAr,
    List<DocumentVersionDto>  Versions,
    List<DocumentMetadataDto> MetadataValues,
    List<string>              Tags);

public sealed record DocumentListItemDto(
    Guid     DocumentId,
    string   DocumentNumber,
    string   TitleAr,
    string?  TitleEn,
    string?  DocumentTypeNameAr,
    string?  LibraryNameAr,
    string   StatusCode,
    string   ClassificationCode,
    string?  FileExtension,
    long?    FileSizeBytes,
    bool     IsCheckedOut,
    bool     IsLegalHold,
    DateTime CreatedAt,
    string?  CreatedByNameAr,
    Guid?    PrimaryWorkspaceId);

public sealed record DocumentVersionDto(
    int      VersionId,
    string   VersionNumber,
    int      MajorVersion,
    int      MinorVersion,
    string   StorageKey,
    string   OriginalFileName,
    string   FileExtension,
    long     FileSizeBytes,
    string   ContentHash,
    string?  ChangeNote,
    string?  CheckInNote,
    bool     IsCurrent,
    DateTime CreatedAt,
    string?  CreatedByNameAr);

public sealed record DocumentMetadataDto(
    int     FieldId,
    string  FieldCode,
    string  LabelAr,
    string  LabelEn,
    string  FieldType,
    string? Value,
    string? DisplayValue,
    string  SourceType);

public sealed record DownloadFileDto(
    string StorageKey,
    string FileName,
    string ContentType,
    bool   RequiresWatermark,
    string DocumentNumber);

namespace Darah.ECM.Application.Documents.Queries;

// ─── GET BY ID ────────────────────────────────────────────────────────────────
public sealed record GetDocumentByIdQuery(Guid DocumentId)
    : IRequest<ApiResponse<DocumentDto>>;

// ─── SEARCH ───────────────────────────────────────────────────────────────────
public sealed record SearchDocumentsQuery(
    string?   TextQuery,
    int?      DocumentTypeId,
    int?      LibraryId,
    int?      FolderId,
    string?   StatusCode,
    int?      ClassificationOrder,
    int?      CreatedBy,
    DateTime? DateFrom,
    DateTime? DateTo,
    bool?     IsLegalHold,
    List<int>? TagIds,
    Guid?     WorkspaceId,
    string?   ExternalSystemId,
    string?   ExternalObjectId,
    string    SortBy        = "CreatedAt",
    string    SortDirection = "DESC",
    int       Page          = 1,
    int       PageSize      = 20) : IRequest<ApiResponse<PagedResult<DocumentListItemDto>>>;

// ─── VERSIONS ─────────────────────────────────────────────────────────────────
public sealed record GetDocumentVersionsQuery(Guid DocumentId)
    : IRequest<ApiResponse<List<DocumentVersionDto>>>;

// ─── DOWNLOAD ─────────────────────────────────────────────────────────────────
public sealed record GetDocumentDownloadQuery(Guid DocumentId, int? VersionId)
    : IRequest<ApiResponse<DownloadFileDto>>;
