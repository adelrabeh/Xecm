namespace Darah.ECM.xECM.Application.DTOs;

public sealed record WorkspaceDto(
    Guid WorkspaceId, string WorkspaceNumber, string TitleAr, string? TitleEn,
    string? TypeCode, string? TypeNameAr, string? StatusAr, string? ClassificationAr,
    string? OwnerNameAr, string? DepartmentAr,
    bool IsBoundToExternal, string? ExternalSystemId, string? ExternalObjectId,
    string? ExternalObjectType, string? ExternalObjectUrl,
    string? SyncStatus, DateTime? LastSyncedAt, bool IsLegalHold,
    DateOnly? RetentionExpiresAt, int DocumentCount, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record WorkspaceListItemDto(
    Guid WorkspaceId, string WorkspaceNumber, string TitleAr, string? TitleEn,
    string? TypeCode, string? TypeNameAr, string? StatusAr,
    string? ExternalSystemId, string? ExternalObjectId, string? SyncStatus,
    bool IsLegalHold, int DocumentCount, DateTime CreatedAt);

public sealed record SyncResultDto(bool IsSuccess, int FieldsUpdated, int ConflictsDetected, string? ErrorMessage, long DurationMs);
public sealed record SyncEventLogDto(long LogId, string EventType, string Direction, string? ExternalObjectId, bool IsSuccessful, string? ErrorMessage, long? DurationMs, DateTime CreatedAt);
public sealed record MetadataSyncMappingDto(int MappingId, string ExternalObjectType, string ExternalFieldName, string ExternalFieldType, int InternalFieldId, string? InternalFieldCode, string? InternalFieldLabelAr, string SyncDirection, string? TransformExpression, string ConflictStrategy, bool IsActive);
