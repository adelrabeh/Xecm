using Darah.ECM.Application.Common.Models;
using Darah.ECM.xECM.Application.Commands;
using MediatR;

namespace Darah.ECM.xECM.Application.Queries;

// ─── WORKSPACE QUERIES ────────────────────────────────────────────────────────
public sealed record GetWorkspaceByIdQuery(Guid WorkspaceId)
    : IRequest<ApiResponse<WorkspaceDto>>;

public sealed record GetWorkspaceByExternalObjectQuery(
    string ExternalSystemCode, string ExternalObjectId)
    : IRequest<ApiResponse<WorkspaceDto>>;

public sealed record ListWorkspacesQuery(
    int?    WorkspaceTypeId     = null,
    string? StatusCode          = null,
    int?    OwnerId             = null,
    int?    DepartmentId        = null,
    string? ExternalSystemCode  = null,
    bool?   IsLegalHold         = null,
    bool?   IsBoundToExternal   = null,
    string? SyncStatus          = null,
    string? TextSearch          = null,
    string  SortBy              = "CreatedAt",
    string  SortDirection       = "DESC",
    int     Page                = 1,
    int     PageSize            = 20)
    : IRequest<ApiResponse<PagedResult<WorkspaceListItemDto>>>;

// Workspace documents
public sealed record GetWorkspaceDocumentsQuery(
    Guid   WorkspaceId,
    string? BindingType = null,
    string? StatusCode  = null,
    string  SortBy      = "BoundAt",
    string  SortDir     = "DESC",
    int     Page        = 1,
    int     PageSize    = 20)
    : IRequest<ApiResponse<PagedResult<WorkspaceDocumentDto>>>;

// Workspace audit log
public sealed record GetWorkspaceAuditLogQuery(
    Guid   WorkspaceId,
    string? EventType = null,
    DateTime? DateFrom = null,
    DateTime? DateTo   = null,
    int    Page        = 1,
    int    PageSize    = 50)
    : IRequest<ApiResponse<PagedResult<WorkspaceAuditLogDto>>>;

// Sync history
public sealed record GetWorkspaceSyncHistoryQuery(
    Guid WorkspaceId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<SyncEventLogDto>>>;

// Workspace metadata
public sealed record GetWorkspaceMetadataQuery(Guid WorkspaceId)
    : IRequest<ApiResponse<List<WorkspaceMetadataValueDto>>>;

// Sync conflict list
public sealed record GetSyncConflictsQuery(Guid WorkspaceId)
    : IRequest<ApiResponse<List<SyncConflictDto>>>;

// External systems
public sealed record ListExternalSystemsQuery(bool? IsActive = null)
    : IRequest<ApiResponse<List<ExternalSystemDto>>>;

public sealed record GetSyncMappingsQuery(int ExternalSystemId, string? ObjectType = null)
    : IRequest<ApiResponse<List<SyncMappingDto>>>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public sealed record WorkspaceAuditLogDto(
    long LogId, Guid WorkspaceId, string EventType,
    int? UserId, string? Username, string? Details,
    string? OldValues, string? NewValues, string? CorrelationId,
    string Severity, DateTime CreatedAt);

public sealed record SyncEventLogDto(
    long LogId, Guid WorkspaceId, int ExternalSystemId,
    string TriggerType, string SyncDirection, string? ExternalObjectId,
    bool IsSuccessful, int FieldsUpdated, int ConflictsDetected,
    string? ErrorMessage, long DurationMs, DateTime CreatedAt);

public sealed record WorkspaceMetadataValueDto(
    int FieldId, string FieldCode, string LabelAr, string LabelEn,
    string FieldType, string? Value, string? DisplayValue, string SourceType,
    DateTime? ExternalSyncedAt);

public sealed record SyncConflictDto(
    Guid WorkspaceId, int FieldId, string FieldCode, string FieldLabelAr,
    string ExternalValue, string InternalValue, string ConflictStrategy,
    DateTime DetectedAt);

public sealed record ExternalSystemDto(
    int SystemId, string SystemCode, string NameAr, string NameEn,
    string SystemType, string BaseUrl, string? AuthType, bool IsActive,
    bool TestMode, DateTime? LastTestedAt, bool? LastTestResult);

public sealed record SyncMappingDto(
    int MappingId, int ExternalSystemId, string? WorkspaceTypeCode,
    string ExternalObjectType, string ExternalFieldName, string ExternalFieldType,
    int InternalFieldId, string? InternalFieldCode, string? InternalFieldLabelAr,
    string SyncDirection, string ConflictStrategy, string? TransformExpression,
    bool IsActive, int SortOrder);
