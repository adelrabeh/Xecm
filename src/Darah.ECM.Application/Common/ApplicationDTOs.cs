namespace Darah.ECM.Application.Common.Models;

// ─── FILE UPLOAD ──────────────────────────────────────────────────────────────
public sealed class FileUploadRequest : IDisposable
{
    public string FileName    { get; }
    public string ContentType { get; }
    public long   Length      { get; }
    public Stream Content     { get; }

    public FileUploadRequest(string fileName, string contentType, long length, Stream content)
    { FileName = fileName; ContentType = contentType; Length = length; Content = content; }

    public void Dispose() => Content.Dispose();
}

// ─── DOCUMENT DTOs ───────────────────────────────────────────────────────────
public sealed record DocumentListItemDto(
    Guid DocumentId, string DocumentNumber, string TitleAr, string? TitleEn,
    string? DocumentTypeNameAr, string? LibraryNameAr, string StatusCode,
    string ClassificationCode, string? FileExtension, long? FileSizeBytes,
    bool IsCheckedOut, bool IsLegalHold, DateTime CreatedAt,
    string? CreatedByNameAr, Guid? PrimaryWorkspaceId);

public sealed record FolderDto(
    int FolderId, string NameAr, string? NameEn, int LibraryId,
    int? ParentFolderId, string Path, int DepthLevel, int SortOrder,
    DateTime CreatedAt, List<FolderDto>? Children = null, int DocumentCount = 0);

public sealed record BreadcrumbItem(int FolderId, string NameAr, string? NameEn, string Path);

public sealed record FolderContentsDto(
    FolderDto? CurrentFolder, List<FolderDto> SubFolders,
    List<DocumentListItemDto> Documents, int TotalDocuments, List<BreadcrumbItem> Breadcrumb);

// ─── WORKFLOW DTOs ────────────────────────────────────────────────────────────
public sealed record WorkflowInstanceDto(
    int InstanceId, Guid DocumentId, string Status, int DefinitionId,
    int? CurrentStepId, int Priority, DateTime StartedAt, DateTime? CompletedAt,
    List<WorkflowTaskDto> Tasks);

public sealed record WorkflowTaskDto(
    int TaskId, int InstanceId, int StepId, string StepNameAr, string Status,
    int? AssignedToUserId, string? AssignedToNameAr, int? AssignedToRoleId,
    bool IsOverdue, DateTime CreatedAt, DateTime DueAt, DateTime? CompletedAt,
    bool IsDelegated, int? DelegatedFrom);

public sealed record WorkflowActionDto(
    int ActionId, int TaskId, int UserId, string? UserNameAr,
    string Action, string? Comment, DateTime CreatedAt);

public sealed record InboxItemDto(
    int TaskId, int InstanceId, string DocumentNumber, string DocumentTitleAr,
    string StepNameAr, string Status, DateTime DueAt, bool IsOverdue,
    int Priority, string? DocumentTypeName, Guid DocumentId);

public sealed record WorkflowSummaryDto(
    int TotalPending, int TotalOverdue, int TotalCompletedToday,
    int TotalCompletedThisWeek, List<InboxItemDto> MyTasks);

public sealed record WorkflowDefinitionDto(
    int DefinitionId, string NameAr, string? NameEn, string TriggerType,
    bool IsActive, int StepCount, List<WorkflowStepDto> Steps);

public sealed record WorkflowStepDto(
    int StepId, int DefinitionId, int StepOrder, string NameAr,
    string? NameEn, int? AssignedToRoleId, int? AssignedToUserId,
    int SLAHours, bool IsParallel);

// ─── RECORDS DTOs ─────────────────────────────────────────────────────────────
public sealed record RecordDeclarationDto(
    Guid DocumentId, string DocumentNumber, int RecordClassId,
    string RetentionPolicyName, DateOnly RetentionExpiryDate, string DisposalAction);

public sealed record LegalHoldResultDto(
    int HoldId, string HoldName, int DocumentsAffected, int DocumentsSkipped);

public sealed record DisposalRequestDto(
    int RequestId, string DisposalCode, string DisposalType,
    string Status, int DocumentCount, DateTime CreatedAt,
    string? CreatedByNameAr = null, string? Justification = null);

// ─── METADATA DTOs ────────────────────────────────────────────────────────────
public sealed record MetadataFieldDto(
    int FieldId, string FieldCode, string LabelAr, string LabelEn,
    string FieldType, bool IsRequired, bool IsSearchable, int SortOrder,
    string? DefaultValue, IEnumerable<string>? LookupValues = null);
