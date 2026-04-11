// ============================================================
// FILE: src/Application/Common/ApiResponse.cs
// ============================================================
namespace Darah.ECM.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new() { Success = true, Data = data, Message = message };
    public static ApiResponse<T> Fail(string message, List<string>? errors = null) => new() { Success = false, Message = message, Errors = errors ?? new() };
    public static ApiResponse<T> ValidationFail(List<string> errors) => new() { Success = false, Message = "Validation failed", Errors = errors };
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

// ============================================================
// FILE: src/Application/Common/Interfaces/ICurrentUser.cs
// ============================================================
namespace Darah.ECM.Application.Common.Interfaces;

public interface ICurrentUser
{
    int UserId { get; }
    string Username { get; }
    string Email { get; }
    string FullNameAr { get; }
    string? FullNameEn { get; }
    string Language { get; }
    string? IPAddress { get; }
    string? SessionId { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Permissions { get; }
    bool HasPermission(string permission);
}

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentVersion> DocumentVersions { get; }
    DbSet<DocumentFile> DocumentFiles { get; }
    DbSet<DocumentLibrary> DocumentLibraries { get; }
    DbSet<Folder> Folders { get; }
    DbSet<DocumentType> DocumentTypes { get; }
    DbSet<MetadataField> MetadataFields { get; }
    DbSet<DocumentMetadataValue> DocumentMetadataValues { get; }
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStep> WorkflowSteps { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<WorkflowTask> WorkflowTasks { get; }
    DbSet<WorkflowAction> WorkflowActions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Tag> Tags { get; }
    DbSet<RetentionPolicy> RetentionPolicies { get; }
    DbSet<LegalHold> LegalHolds { get; }
    DbSet<SavedSearch> SavedSearches { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task LogAsync(string eventType, string? entityType = null, string? entityId = null, object? oldValues = null, object? newValues = null, string severity = "Info", bool isSuccessful = true, string? failureReason = null, string? additionalInfo = null);
}

public interface IFileStorageService
{
    Task<string> StoreAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream> RetrieveAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
    string GetProvider();
}

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, string? toName = null, CancellationToken ct = default);
}

public interface IDocumentNumberGenerator
{
    Task<string> GenerateAsync(int documentTypeId, CancellationToken ct = default);
}

public interface IWorkflowEngine
{
    Task<int> StartAsync(Guid documentId, int definitionId, int initiatedBy, CancellationToken ct = default);
    Task<bool> ProcessActionAsync(int taskId, WorkflowActionType action, int actionBy, string? comment = null, int? delegateToUserId = null, CancellationToken ct = default);
    Task CheckSLABreachesAsync(CancellationToken ct = default);
    Task<IEnumerable<WorkflowTask>> GetUserInboxAsync(int userId, CancellationToken ct = default);
}

// ============================================================
// FILE: src/Application/Documents/Commands/UploadDocumentCommand.cs
// ============================================================
namespace Darah.ECM.Application.Documents.Commands;

public record UploadDocumentCommand : IRequest<ApiResponse<DocumentDto>>
{
    public string TitleAr { get; init; } = string.Empty;
    public string? TitleEn { get; init; }
    public int DocumentTypeId { get; init; }
    public int LibraryId { get; init; }
    public int? FolderId { get; init; }
    public int? RetentionPolicyId { get; init; }
    public int ClassificationLevelId { get; init; } = 1;
    public DateOnly? DocumentDate { get; init; }
    public string? Keywords { get; init; }
    public string? Summary { get; init; }
    public string? SourceReference { get; init; }
    public IFormFile File { get; init; } = null!;
    public Dictionary<int, string> MetadataValues { get; init; } = new();
    public List<int> TagIds { get; init; } = new();
}

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(500).WithMessage("عنوان الوثيقة مطلوب");
        RuleFor(x => x.DocumentTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع الوثيقة");
        RuleFor(x => x.LibraryId).GreaterThan(0).WithMessage("يجب اختيار المكتبة");
        RuleFor(x => x.ClassificationLevelId).InclusiveBetween(1, 4);
        RuleFor(x => x.File).NotNull().WithMessage("يجب رفع ملف");
        RuleFor(x => x.File.Length).LessThanOrEqualTo(512L * 1024 * 1024).When(x => x.File != null).WithMessage("حجم الملف يتجاوز الحد المسموح (512 MB)");
    }
}

public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, ApiResponse<DocumentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditService _audit;
    private readonly IDocumentNumberGenerator _numberGenerator;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".mp4", ".zip"
    };

    public UploadDocumentCommandHandler(IApplicationDbContext context, ICurrentUser currentUser, IFileStorageService fileStorage, IAuditService audit, IDocumentNumberGenerator numberGenerator)
    {
        _context = context; _currentUser = currentUser; _fileStorage = fileStorage; _audit = audit; _numberGenerator = numberGenerator;
    }

    public async Task<ApiResponse<DocumentDto>> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        // Validate extension
        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return ApiResponse<DocumentDto>.Fail($"نوع الملف '{ext}' غير مدعوم");

        // Validate library access
        var library = await _context.DocumentLibraries.FirstOrDefaultAsync(l => l.LibraryId == request.LibraryId && !l.IsDeleted, ct);
        if (library == null) return ApiResponse<DocumentDto>.Fail("المكتبة غير موجودة");

        // Generate document number
        var docNumber = await _numberGenerator.GenerateAsync(request.DocumentTypeId, ct);

        // Get default draft status
        var draftStatus = await _context.LookupValues
            .Where(lv => lv.Category.Code == "DOC_STATUS" && lv.Code == "DRAFT")
            .Select(lv => lv.ValueId).FirstOrDefaultAsync(ct);

        // Store file
        using var stream = request.File.OpenReadStream();
        var storageKey = await _fileStorage.StoreAsync(stream, request.File.FileName, request.File.ContentType, ct);

        // Compute hash for integrity
        var hash = await ComputeSha256Async(request.File, ct);

        // Create file record
        var docFile = new DocumentFile
        {
            StorageKey = storageKey,
            OriginalFileName = request.File.FileName,
            ContentType = request.File.ContentType,
            FileExtension = ext,
            FileSizeBytes = request.File.Length,
            ContentHash = hash,
            StorageProvider = _fileStorage.GetProvider(),
        };
        docFile.SetCreated(_currentUser.UserId);
        _context.DocumentFiles.Add(docFile);

        // Create document
        var document = Document.Create(request.TitleAr, request.DocumentTypeId, request.LibraryId, draftStatus, _currentUser.UserId, docNumber, request.TitleEn, request.FolderId, request.RetentionPolicyId, request.ClassificationLevelId, request.DocumentDate, request.Keywords, request.Summary);
        _context.Documents.Add(document);

        await _context.SaveChangesAsync(ct);

        // Create initial version
        var version = DocumentVersion.Create(document.DocumentId, "1.0", 1, 0, docFile.FileId, request.File.FileName, request.File.Length, hash, "Initial version", _currentUser.UserId);
        _context.DocumentVersions.Add(version);
        await _context.SaveChangesAsync(ct);

        // Update document with version pointer
        document.CheckIn(version.VersionId, _currentUser.UserId);

        // Save metadata values
        foreach (var (fieldId, value) in request.MetadataValues)
        {
            var metaValue = new DocumentMetadataValue { DocumentId = document.DocumentId, FieldId = fieldId, TextValue = value };
            _context.DocumentMetadataValues.Add(metaValue);
        }

        // Save tags
        foreach (var tagId in request.TagIds)
        {
            _context.DocumentTags.Add(new DocumentTag { DocumentId = document.DocumentId, TagId = tagId, AddedBy = _currentUser.UserId });
        }

        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync("DocumentCreated", "Document", document.DocumentId.ToString(), new { }, new { document.DocumentNumber, document.TitleAr });

        var dto = await MapToDto(document, version, docFile, ct);
        return ApiResponse<DocumentDto>.Ok(dto, "تم رفع الوثيقة بنجاح");
    }

    private async Task<string> ComputeSha256Async(IFormFile file, CancellationToken ct)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = file.OpenReadStream();
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLower();
    }

    private Task<DocumentDto> MapToDto(Document doc, DocumentVersion version, DocumentFile file, CancellationToken ct)
    {
        return Task.FromResult(new DocumentDto
        {
            DocumentId = doc.DocumentId,
            DocumentNumber = doc.DocumentNumber,
            TitleAr = doc.TitleAr,
            TitleEn = doc.TitleEn,
            CurrentVersion = version.VersionNumber,
            FileSizeBytes = file.FileSizeBytes,
            FileExtension = file.FileExtension,
            CreatedAt = doc.CreatedAt,
            IsCheckedOut = doc.IsCheckedOut,
            IsLegalHold = doc.IsLegalHold
        });
    }
}

// ============================================================
// FILE: src/Application/Documents/DTOs/DocumentDto.cs
// ============================================================
namespace Darah.ECM.Application.Documents.DTOs;

public class DocumentDto
{
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? DocumentTypeNameAr { get; set; }
    public string? DocumentTypeNameEn { get; set; }
    public string? LibraryNameAr { get; set; }
    public string? FolderNameAr { get; set; }
    public string? StatusAr { get; set; }
    public string? StatusEn { get; set; }
    public string? ClassificationAr { get; set; }
    public int ClassificationLevel { get; set; }
    public string? CurrentVersion { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? FileExtension { get; set; }
    public string? ContentType { get; set; }
    public bool IsCheckedOut { get; set; }
    public bool IsLegalHold { get; set; }
    public string? CheckedOutByName { get; set; }
    public DateTime? RetentionExpiresAt { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public string? Keywords { get; set; }
    public string? Summary { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByNameAr { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<DocumentVersionDto> Versions { get; set; } = new();
    public List<MetadataValueDto> MetadataValues { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class DocumentVersionDto
{
    public int VersionId { get; set; }
    public string VersionNumber { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? ChangeNote { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByNameAr { get; set; }
}

public class MetadataValueDto
{
    public int FieldId { get; set; }
    public string FieldCode { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? DisplayValue { get; set; }
}

// ============================================================
// FILE: src/Application/Workflow/Commands/SubmitToWorkflowCommand.cs
// ============================================================
namespace Darah.ECM.Application.Workflow.Commands;

public record SubmitToWorkflowCommand : IRequest<ApiResponse<WorkflowInstanceDto>>
{
    public Guid DocumentId { get; init; }
    public int? WorkflowDefinitionId { get; init; }  // null = auto-detect from document type
    public int Priority { get; init; } = 2;
    public string? Comment { get; init; }
}

public class SubmitToWorkflowCommandHandler : IRequestHandler<SubmitToWorkflowCommand, ApiResponse<WorkflowInstanceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IAuditService _audit;

    public SubmitToWorkflowCommandHandler(IApplicationDbContext context, ICurrentUser currentUser, IWorkflowEngine workflowEngine, IAuditService audit)
    {
        _context = context; _currentUser = currentUser; _workflowEngine = workflowEngine; _audit = audit;
    }

    public async Task<ApiResponse<WorkflowInstanceDto>> Handle(SubmitToWorkflowCommand request, CancellationToken ct)
    {
        var document = await _context.Documents.Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.DocumentId == request.DocumentId && !d.IsDeleted, ct);

        if (document == null) return ApiResponse<WorkflowInstanceDto>.Fail("الوثيقة غير موجودة");
        if (document.IsCheckedOut) return ApiResponse<WorkflowInstanceDto>.Fail("الوثيقة محجوزة. يجب إيداعها أولاً");
        if (document.IsLegalHold) return ApiResponse<WorkflowInstanceDto>.Fail("الوثيقة خاضعة لتجميد قانوني");

        // Detect workflow definition
        var definitionId = request.WorkflowDefinitionId;
        if (!definitionId.HasValue)
        {
            definitionId = await _context.WorkflowDefinitions
                .Where(wd => wd.IsActive && (wd.DocumentTypeId == null || wd.DocumentTypeId == document.DocumentTypeId) && wd.IsDefault)
                .Select(wd => (int?)wd.DefinitionId).FirstOrDefaultAsync(ct);
        }

        if (!definitionId.HasValue) return ApiResponse<WorkflowInstanceDto>.Fail("لا يوجد مسار عمل محدد لهذا النوع من الوثائق");

        // Check no active workflow instance
        var existingInstance = await _context.WorkflowInstances
            .AnyAsync(wi => wi.DocumentId == request.DocumentId && wi.Status == "InProgress", ct);
        if (existingInstance) return ApiResponse<WorkflowInstanceDto>.Fail("يوجد مسار عمل نشط لهذه الوثيقة بالفعل");

        var instanceId = await _workflowEngine.StartAsync(request.DocumentId, definitionId.Value, _currentUser.UserId, ct);

        // Update document status to Pending
        var pendingStatus = await _context.LookupValues
            .Where(lv => lv.Category.Code == "DOC_STATUS" && lv.Code == "PENDING")
            .Select(lv => lv.ValueId).FirstOrDefaultAsync(ct);
        document.UpdateStatus(pendingStatus, _currentUser.UserId);

        await _context.SaveChangesAsync(ct);
        await _audit.LogAsync("WorkflowSubmitted", "WorkflowInstance", instanceId.ToString(), additionalInfo: $"DocumentId: {request.DocumentId}, Priority: {request.Priority}");

        var instance = await _context.WorkflowInstances.FirstAsync(wi => wi.InstanceId == instanceId, ct);
        return ApiResponse<WorkflowInstanceDto>.Ok(new WorkflowInstanceDto { InstanceId = instanceId, Status = instance.Status, StartedAt = instance.StartedAt }, "تم إرسال الوثيقة لمسار الاعتماد بنجاح");
    }
}

public record WorkflowActionCommand : IRequest<ApiResponse<bool>>
{
    public int TaskId { get; init; }
    public string ActionType { get; init; } = string.Empty;  // Approve, Reject, Return, Delegate
    public string? Comment { get; init; }
    public int? DelegateToUserId { get; init; }
}

public class WorkflowActionCommandHandler : IRequestHandler<WorkflowActionCommand, ApiResponse<bool>>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;

    public WorkflowActionCommandHandler(IWorkflowEngine workflowEngine, ICurrentUser currentUser, IAuditService audit)
    {
        _workflowEngine = workflowEngine; _currentUser = currentUser; _audit = audit;
    }

    public async Task<ApiResponse<bool>> Handle(WorkflowActionCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<WorkflowActionType>(request.ActionType, true, out var actionType))
            return ApiResponse<bool>.Fail("نوع الإجراء غير صحيح");

        var success = await _workflowEngine.ProcessActionAsync(request.TaskId, actionType, _currentUser.UserId, request.Comment, request.DelegateToUserId, ct);

        if (!success) return ApiResponse<bool>.Fail("فشل في تنفيذ الإجراء. تأكد من صلاحياتك والمهمة المعينة لك");

        await _audit.LogAsync($"WorkflowAction_{request.ActionType}", "WorkflowTask", request.TaskId.ToString(), additionalInfo: $"Comment: {request.Comment}");

        return ApiResponse<bool>.Ok(true, $"تم تنفيذ الإجراء '{request.ActionType}' بنجاح");
    }
}

public class WorkflowInstanceDto
{
    public int InstanceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<WorkflowTaskDto> Tasks { get; set; } = new();
}

public class WorkflowTaskDto
{
    public int TaskId { get; set; }
    public string StepNameAr { get; set; } = string.Empty;
    public string StepNameEn { get; set; } = string.Empty;
    public string? AssignedToNameAr { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public bool IsOverdue { get; set; }
    public List<WorkflowActionDto> Actions { get; set; } = new();
}

public class WorkflowActionDto
{
    public string ActionType { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime ActionAt { get; set; }
    public string? ActionByNameAr { get; set; }
}

// ============================================================
// FILE: src/Application/Search/Queries/AdvancedSearchQuery.cs
// ============================================================
namespace Darah.ECM.Application.Search.Queries;

public record AdvancedSearchQuery : IRequest<ApiResponse<PagedResult<DocumentDto>>>
{
    public string? TextQuery { get; init; }
    public int? DocumentTypeId { get; init; }
    public int? LibraryId { get; init; }
    public int? FolderId { get; init; }
    public int? StatusValueId { get; init; }
    public int? ClassificationLevelId { get; init; }
    public int? CreatedBy { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public DateOnly? DocumentDateFrom { get; init; }
    public DateOnly? DocumentDateTo { get; init; }
    public bool? IsLegalHold { get; init; }
    public List<int> TagIds { get; init; } = new();
    public Dictionary<int, string> MetadataFilters { get; init; } = new();
    public string SortBy { get; init; } = "CreatedAt";
    public string SortDirection { get; init; } = "DESC";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class AdvancedSearchQueryHandler : IRequestHandler<AdvancedSearchQuery, ApiResponse<PagedResult<DocumentDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public AdvancedSearchQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context; _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<DocumentDto>>> Handle(AdvancedSearchQuery request, CancellationToken ct)
    {
        var query = _context.Documents
            .Where(d => !d.IsDeleted)
            .Include(d => d.DocumentType)
            .Include(d => d.Library)
            .Include(d => d.Folder)
            .AsQueryable();

        // Apply filters
        if (request.DocumentTypeId.HasValue) query = query.Where(d => d.DocumentTypeId == request.DocumentTypeId.Value);
        if (request.LibraryId.HasValue) query = query.Where(d => d.LibraryId == request.LibraryId.Value);
        if (request.FolderId.HasValue) query = query.Where(d => d.FolderId == request.FolderId.Value);
        if (request.StatusValueId.HasValue) query = query.Where(d => d.StatusValueId == request.StatusValueId.Value);
        if (request.ClassificationLevelId.HasValue) query = query.Where(d => d.ClassificationLevelId == request.ClassificationLevelId.Value);
        if (request.CreatedBy.HasValue) query = query.Where(d => d.CreatedBy == request.CreatedBy.Value);
        if (request.IsLegalHold.HasValue) query = query.Where(d => d.IsLegalHold == request.IsLegalHold.Value);
        if (request.DateFrom.HasValue) query = query.Where(d => d.CreatedAt >= request.DateFrom.Value);
        if (request.DateTo.HasValue) query = query.Where(d => d.CreatedAt <= request.DateTo.Value);
        if (request.DocumentDateFrom.HasValue) query = query.Where(d => d.DocumentDate >= request.DocumentDateFrom.Value);
        if (request.DocumentDateTo.HasValue) query = query.Where(d => d.DocumentDate <= request.DocumentDateTo.Value);

        // Text search (Full-Text Search via CONTAINS or LIKE fallback)
        if (!string.IsNullOrWhiteSpace(request.TextQuery))
        {
            var q = request.TextQuery.Trim();
            query = query.Where(d =>
                EF.Functions.Contains(d.TitleAr, q) ||
                EF.Functions.Contains(d.Keywords, q) ||
                d.TitleEn!.Contains(q) ||
                d.DocumentNumber.Contains(q));
        }

        // Tag filter
        if (request.TagIds.Any())
        {
            var taggedDocIds = await _context.DocumentTags
                .Where(dt => request.TagIds.Contains(dt.TagId))
                .Select(dt => dt.DocumentId).Distinct().ToListAsync(ct);
            query = query.Where(d => taggedDocIds.Contains(d.DocumentId));
        }

        // Sort
        query = (request.SortBy?.ToLower(), request.SortDirection?.ToUpper()) switch
        {
            ("titlear", "ASC") => query.OrderBy(d => d.TitleAr),
            ("titlear", _) => query.OrderByDescending(d => d.TitleAr),
            ("documentdate", "ASC") => query.OrderBy(d => d.DocumentDate),
            ("documentdate", _) => query.OrderByDescending(d => d.DocumentDate),
            ("createdat", "ASC") => query.OrderBy(d => d.CreatedAt),
            _ => query.OrderByDescending(d => d.CreatedAt),
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var dtos = items.Select(d => new DocumentDto
        {
            DocumentId = d.DocumentId,
            DocumentNumber = d.DocumentNumber,
            TitleAr = d.TitleAr,
            TitleEn = d.TitleEn,
            DocumentTypeNameAr = d.DocumentType?.NameAr,
            LibraryNameAr = d.Library?.NameAr,
            FolderNameAr = d.Folder?.NameAr,
            IsCheckedOut = d.IsCheckedOut,
            IsLegalHold = d.IsLegalHold,
            CreatedAt = d.CreatedAt,
        }).ToList();

        return ApiResponse<PagedResult<DocumentDto>>.Ok(new PagedResult<DocumentDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
