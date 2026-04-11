// ============================================================
// FILE: src/API/Controllers/AuthController.cs
// ============================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Darah.ECM.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthService _authService;
    private readonly IAuditService _audit;

    public AuthController(IMediator mediator, IAuthService authService, IAuditService audit)
    {
        _mediator = mediator;
        _authService = authService;
        _audit = audit;
    }

    /// <summary>User login — returns JWT access token + sets HttpOnly refresh cookie</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password, GetClientIp(), GetUserAgent());
        if (!result.Success) return Unauthorized(result);

        // Set HttpOnly refresh token cookie
        Response.Cookies.Append("ecm_refresh", result.Data!.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(8),
            Path = "/api/v1/auth"
        });

        // Don't expose refresh token in body
        result.Data.RefreshToken = string.Empty;
        return Ok(result);
    }

    /// <summary>Refresh access token using HttpOnly cookie</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Refresh()
    {
        var refreshToken = Request.Cookies["ecm_refresh"];
        if (string.IsNullOrEmpty(refreshToken)) return Unauthorized(ApiResponse<LoginResponseDto>.Fail("جلسة منتهية. الرجاء تسجيل الدخول مجدداً"));

        var result = await _authService.RefreshTokenAsync(refreshToken, GetClientIp());
        if (!result.Success) return Unauthorized(result);

        Response.Cookies.Append("ecm_refresh", result.Data!.RefreshToken, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(8), Path = "/api/v1/auth"
        });
        result.Data.RefreshToken = string.Empty;
        return Ok(result);
    }

    /// <summary>Logout — revoke session and clear cookie</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout()
    {
        var refreshToken = Request.Cookies["ecm_refresh"];
        if (!string.IsNullOrEmpty(refreshToken))
            await _authService.RevokeSessionAsync(refreshToken);

        Response.Cookies.Delete("ecm_refresh", new CookieOptions { Path = "/api/v1/auth" });
        await _audit.LogAsync("UserLogout");
        return Ok(ApiResponse<bool>.Ok(true, "تم تسجيل الخروج"));
    }

    /// <summary>Change password</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _authService.ChangePasswordAsync(request.CurrentPassword, request.NewPassword);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Get current user profile</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile()
    {
        var result = await _authService.GetProfileAsync();
        return Ok(result);
    }

    private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    private string GetUserAgent() => Request.Headers.UserAgent.ToString();
}

// ============================================================
// FILE: src/API/Controllers/DocumentsController.cs
// ============================================================
[ApiController]
[Route("api/v1/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorageService _fileStorage;

    public DocumentsController(IMediator mediator, ICurrentUser currentUser, IFileStorageService fileStorage)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    /// <summary>List documents with filtering and pagination</summary>
    [HttpGet]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentDto>>>> GetDocuments(
        [FromQuery] int? libraryId,
        [FromQuery] int? folderId,
        [FromQuery] int? documentTypeId,
        [FromQuery] int? statusValueId,
        [FromQuery] string? search,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] string sortDir = "DESC",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdvancedSearchQuery
        {
            LibraryId = libraryId, FolderId = folderId,
            DocumentTypeId = documentTypeId, StatusValueId = statusValueId,
            TextQuery = search, SortBy = sortBy, SortDirection = sortDir,
            Page = page, PageSize = Math.Min(pageSize, 100)
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Get document details by ID</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> GetDocument(Guid id)
    {
        var result = await _mediator.Send(new GetDocumentByIdQuery { DocumentId = id });
        if (result.Data == null) return NotFound(ApiResponse<DocumentDto>.Fail("الوثيقة غير موجودة"));
        return Ok(result);
    }

    /// <summary>Upload a new document with file and metadata</summary>
    [HttpPost]
    [RequirePermission("documents.create")]
    [RequestSizeLimit(536870912)] // 512 MB
    public async Task<ActionResult<ApiResponse<DocumentDto>>> UploadDocument([FromForm] UploadDocumentCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success) return BadRequest(result);
        return CreatedAtAction(nameof(GetDocument), new { id = result.Data!.DocumentId }, result);
    }

    /// <summary>Update document metadata</summary>
    [HttpPut("{id:guid}/metadata")]
    [RequirePermission("documents.update")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateMetadata(Guid id, [FromBody] UpdateDocumentMetadataCommand command)
    {
        command = command with { DocumentId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Upload a new version for an existing document</summary>
    [HttpPost("{id:guid}/versions")]
    [RequirePermission("documents.write")]
    [RequestSizeLimit(536870912)]
    public async Task<ActionResult<ApiResponse<DocumentVersionDto>>> AddVersion(Guid id, [FromForm] AddDocumentVersionCommand command)
    {
        command = command with { DocumentId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Get all versions of a document</summary>
    [HttpGet("{id:guid}/versions")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<List<DocumentVersionDto>>>> GetVersions(Guid id)
    {
        var result = await _mediator.Send(new GetDocumentVersionsQuery { DocumentId = id });
        return Ok(result);
    }

    /// <summary>Check out document — locks for editing</summary>
    [HttpPost("{id:guid}/checkout")]
    [RequirePermission("documents.checkout")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckOut(Guid id)
    {
        var result = await _mediator.Send(new CheckOutDocumentCommand { DocumentId = id });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Check in document — releases lock and creates new version</summary>
    [HttpPost("{id:guid}/checkin")]
    [RequirePermission("documents.checkin")]
    [RequestSizeLimit(536870912)]
    public async Task<ActionResult<ApiResponse<bool>>> CheckIn(Guid id, [FromForm] CheckInDocumentCommand command)
    {
        command = command with { DocumentId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Download document file — current or specific version</summary>
    [HttpGet("{id:guid}/download")]
    [RequirePermission("documents.download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] int? versionId = null)
    {
        var result = await _mediator.Send(new GetDocumentDownloadQuery { DocumentId = id, VersionId = versionId });
        if (!result.Success) return NotFound(result.Message);

        var stream = await _fileStorage.RetrieveAsync(result.Data!.StorageKey);
        return File(stream, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>Get document preview URL or stream (PDF/image)</summary>
    [HttpGet("{id:guid}/preview")]
    [RequirePermission("documents.read")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var result = await _mediator.Send(new GetDocumentPreviewQuery { DocumentId = id });
        if (!result.Success) return NotFound();

        var stream = await _fileStorage.RetrieveAsync(result.Data!.StorageKey);
        // Add watermark headers for classified documents
        if (result.Data.RequiresWatermark)
            Response.Headers.Append("X-Watermark", _currentUser.FullNameAr);

        return File(stream, result.Data.ContentType);
    }

    /// <summary>Soft delete a document</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission("documents.delete")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, [FromBody] DeleteDocumentCommand command)
    {
        command = command with { DocumentId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Move document to a different folder</summary>
    [HttpPost("{id:guid}/move")]
    [RequirePermission("documents.update")]
    public async Task<ActionResult<ApiResponse<bool>>> Move(Guid id, [FromBody] MoveDocumentCommand command)
    {
        command = command with { DocumentId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Apply legal hold to document</summary>
    [HttpPost("{id:guid}/legal-hold")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<bool>>> ApplyLegalHold(Guid id, [FromBody] ApplyLegalHoldCommand command)
    {
        command = command with { DocumentId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Get document comments</summary>
    [HttpGet("{id:guid}/comments")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<List<CommentDto>>>> GetComments(Guid id)
    {
        var result = await _mediator.Send(new GetDocumentCommentsQuery { DocumentId = id });
        return Ok(result);
    }

    /// <summary>Add a comment to a document</summary>
    [HttpPost("{id:guid}/comments")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<CommentDto>>> AddComment(Guid id, [FromBody] AddCommentCommand command)
    {
        command = command with { DocumentId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ============================================================
// FILE: src/API/Controllers/WorkflowController.cs
// ============================================================
[ApiController]
[Route("api/v1/workflow")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkflowController(IMediator mediator) => _mediator = mediator;

    /// <summary>Submit document to workflow</summary>
    [HttpPost("submit/{documentId:guid}")]
    [RequirePermission("workflow.submit")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceDto>>> Submit(Guid documentId, [FromBody] SubmitToWorkflowCommand command)
    {
        command = command with { DocumentId = documentId };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Get current user's workflow inbox (pending tasks)</summary>
    [HttpGet("inbox")]
    public async Task<ActionResult<ApiResponse<PagedResult<InboxItemDto>>>> GetInbox(
        [FromQuery] string? status = "Pending",
        [FromQuery] bool overdueOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetWorkflowInboxQuery
        {
            Status = status, OverdueOnly = overdueOnly, Page = page, PageSize = pageSize
        });
        return Ok(result);
    }

    /// <summary>Get task details</summary>
    [HttpGet("tasks/{taskId:int}")]
    public async Task<ActionResult<ApiResponse<WorkflowTaskDetailDto>>> GetTask(int taskId)
    {
        var result = await _mediator.Send(new GetWorkflowTaskDetailQuery { TaskId = taskId });
        if (result.Data == null) return NotFound(ApiResponse<WorkflowTaskDetailDto>.Fail("المهمة غير موجودة"));
        return Ok(result);
    }

    /// <summary>Approve a workflow task</summary>
    [HttpPost("tasks/{taskId:int}/approve")]
    [RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Approve(int taskId, [FromBody] WorkflowCommentRequest request)
    {
        var result = await _mediator.Send(new WorkflowActionCommand { TaskId = taskId, ActionType = "Approve", Comment = request.Comment });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Reject a workflow task</summary>
    [HttpPost("tasks/{taskId:int}/reject")]
    [RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Reject(int taskId, [FromBody] WorkflowCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(ApiResponse<bool>.Fail("يجب إدخال سبب الرفض"));
        var result = await _mediator.Send(new WorkflowActionCommand { TaskId = taskId, ActionType = "Reject", Comment = request.Comment });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Return task for revision</summary>
    [HttpPost("tasks/{taskId:int}/return")]
    [RequirePermission("workflow.approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Return(int taskId, [FromBody] WorkflowCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(ApiResponse<bool>.Fail("يجب إدخال سبب الإرجاع"));
        var result = await _mediator.Send(new WorkflowActionCommand { TaskId = taskId, ActionType = "Return", Comment = request.Comment });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Delegate task to another user</summary>
    [HttpPost("tasks/{taskId:int}/delegate")]
    [RequirePermission("workflow.delegate")]
    public async Task<ActionResult<ApiResponse<bool>>> Delegate(int taskId, [FromBody] DelegateTaskRequest request)
    {
        if (request.DelegateToUserId <= 0)
            return BadRequest(ApiResponse<bool>.Fail("يجب تحديد المستخدم للتفويض"));
        var result = await _mediator.Send(new WorkflowActionCommand { TaskId = taskId, ActionType = "Delegate", Comment = request.Comment, DelegateToUserId = request.DelegateToUserId });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Get full workflow history for a document</summary>
    [HttpGet("instances/{instanceId:int}/history")]
    public async Task<ActionResult<ApiResponse<List<WorkflowHistoryDto>>>> GetHistory(int instanceId)
    {
        var result = await _mediator.Send(new GetWorkflowHistoryQuery { InstanceId = instanceId });
        return Ok(result);
    }

    /// <summary>Get workflow definitions (admin)</summary>
    [HttpGet("definitions")]
    [RequirePermission("workflow.manage")]
    public async Task<ActionResult<ApiResponse<List<WorkflowDefinitionDto>>>> GetDefinitions()
    {
        var result = await _mediator.Send(new GetWorkflowDefinitionsQuery());
        return Ok(result);
    }

    /// <summary>Create workflow definition (admin)</summary>
    [HttpPost("definitions")]
    [RequirePermission("workflow.manage")]
    public async Task<ActionResult<ApiResponse<WorkflowDefinitionDto>>> CreateDefinition([FromBody] CreateWorkflowDefinitionCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ============================================================
// FILE: src/API/Controllers/SearchController.cs
// ============================================================
[ApiController]
[Route("api/v1/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchController(IMediator mediator) => _mediator = mediator;

    /// <summary>Quick search — fast, simple text search</summary>
    [HttpGet("quick")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentDto>>>> QuickSearch(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(ApiResponse<PagedResult<DocumentDto>>.Fail("يجب أن يكون النص المراد البحث عنه حرفين على الأقل"));

        var result = await _mediator.Send(new AdvancedSearchQuery { TextQuery = q, Page = page, PageSize = pageSize });
        return Ok(result);
    }

    /// <summary>Advanced search with multiple filters</summary>
    [HttpPost("advanced")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentDto>>>> AdvancedSearch([FromBody] AdvancedSearchQuery query)
    {
        query = query with { PageSize = Math.Min(query.PageSize, 100) };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Get saved searches for current user</summary>
    [HttpGet("saved")]
    public async Task<ActionResult<ApiResponse<List<SavedSearchDto>>>> GetSavedSearches()
    {
        var result = await _mediator.Send(new GetSavedSearchesQuery());
        return Ok(result);
    }

    /// <summary>Save a search query</summary>
    [HttpPost("saved")]
    public async Task<ActionResult<ApiResponse<SavedSearchDto>>> SaveSearch([FromBody] CreateSavedSearchCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Delete a saved search</summary>
    [HttpDelete("saved/{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteSavedSearch(int id)
    {
        var result = await _mediator.Send(new DeleteSavedSearchCommand { SearchId = id });
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ============================================================
// FILE: src/API/Controllers/LibrariesController.cs
// ============================================================
[ApiController]
[Route("api/v1/libraries")]
[Authorize]
public class LibrariesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LibrariesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<LibraryDto>>>> GetAll()
    {
        var result = await _mediator.Send(new GetLibrariesQuery());
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<LibraryDto>>> GetById(int id)
    {
        var result = await _mediator.Send(new GetLibraryByIdQuery { LibraryId = id });
        if (result.Data == null) return NotFound(ApiResponse<LibraryDto>.Fail("المكتبة غير موجودة"));
        return Ok(result);
    }

    [HttpGet("{id:int}/folders")]
    public async Task<ActionResult<ApiResponse<List<FolderDto>>>> GetFolders(int id, [FromQuery] int? parentFolderId = null)
    {
        var result = await _mediator.Send(new GetFoldersQuery { LibraryId = id, ParentFolderId = parentFolderId });
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission("admin.system")]
    public async Task<ActionResult<ApiResponse<LibraryDto>>> Create([FromBody] CreateLibraryCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data!.LibraryId }, result) : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("admin.system")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(int id, [FromBody] UpdateLibraryCommand command)
    {
        command = command with { LibraryId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ============================================================
// FILE: src/API/Controllers/AuditController.cs
// ============================================================
[ApiController]
[Route("api/v1/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get audit logs with filtering and pagination</summary>
    [HttpGet("logs")]
    [RequirePermission("audit.read")]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> GetLogs(
        [FromQuery] string? eventType,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? userId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? severity,
        [FromQuery] bool? isSuccessful,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new GetAuditLogsQuery
        {
            EventType = eventType, EntityType = entityType, EntityId = entityId,
            UserId = userId, DateFrom = dateFrom, DateTo = dateTo,
            Severity = severity, IsSuccessful = isSuccessful, Page = page, PageSize = pageSize
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Export audit logs to Excel</summary>
    [HttpGet("logs/export")]
    [RequirePermission("audit.export")]
    public async Task<IActionResult> ExportLogs(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? eventType)
    {
        var result = await _mediator.Send(new ExportAuditLogsQuery { DateFrom = dateFrom, DateTo = dateTo, EventType = eventType });
        if (!result.Success) return BadRequest(result.Message);
        return File(result.Data!.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"AuditLog_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
    }
}

// ============================================================
// FILE: src/API/Controllers/NotificationsController.cs
// ============================================================
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> GetNotifications(
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetNotificationsQuery { UnreadOnly = unreadOnly, Page = page, PageSize = pageSize });
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount()
    {
        var result = await _mediator.Send(new GetUnreadNotificationCountQuery());
        return Ok(result);
    }

    [HttpPut("{id:long}/read")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkAsRead(long id)
    {
        var result = await _mediator.Send(new MarkNotificationReadCommand { NotificationId = id });
        return Ok(result);
    }

    [HttpPut("mark-all-read")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkAllRead()
    {
        var result = await _mediator.Send(new MarkAllNotificationsReadCommand());
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(long id)
    {
        var result = await _mediator.Send(new DeleteNotificationCommand { NotificationId = id });
        return Ok(result);
    }
}

// ============================================================
// FILE: src/API/Controllers/ReportsController.cs
// ============================================================
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Executive dashboard KPIs</summary>
    [HttpGet("dashboard/executive")]
    [RequirePermission("reports.view")]
    public async Task<ActionResult<ApiResponse<ExecutiveDashboardDto>>> GetExecutiveDashboard()
    {
        var result = await _mediator.Send(new GetExecutiveDashboardQuery());
        return Ok(result);
    }

    /// <summary>Operational dashboard — workflow and document activity</summary>
    [HttpGet("dashboard/operational")]
    [RequirePermission("reports.view")]
    public async Task<ActionResult<ApiResponse<OperationalDashboardDto>>> GetOperationalDashboard(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        var result = await _mediator.Send(new GetOperationalDashboardQuery { DateFrom = dateFrom, DateTo = dateTo });
        return Ok(result);
    }

    /// <summary>Export report to Excel or PDF</summary>
    [HttpPost("export")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> ExportReport([FromBody] ExportReportCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success) return BadRequest(result.Message);
        var contentType = command.Format == "PDF"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var ext = command.Format == "PDF" ? "pdf" : "xlsx";
        return File(result.Data!.Content, contentType, $"{command.ReportCode}_{DateTime.UtcNow:yyyyMMdd}.{ext}");
    }
}

// ============================================================
// FILE: src/API/Controllers/AdminController.cs
// ============================================================
[ApiController]
[Route("api/v1/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    // ── Users ──────────────────────────────────────────────
    [HttpGet("users")]
    [RequirePermission("admin.users")]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GetUsers(
        [FromQuery] string? search, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetUsersQuery { Search = search, IsActive = isActive, Page = page, PageSize = pageSize });
        return Ok(result);
    }

    [HttpGet("users/{id:int}")]
    [RequirePermission("admin.users")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(int id)
    {
        var result = await _mediator.Send(new GetUserByIdQuery { UserId = id });
        if (result.Data == null) return NotFound(ApiResponse<UserDto>.Fail("المستخدم غير موجود"));
        return Ok(result);
    }

    [HttpPost("users")]
    [RequirePermission("admin.users")]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(GetUser), new { id = result.Data!.UserId }, result) : BadRequest(result);
    }

    [HttpPut("users/{id:int}")]
    [RequirePermission("admin.users")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateUser(int id, [FromBody] UpdateUserCommand command)
    {
        command = command with { UserId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{id:int}/assign-role")]
    [RequirePermission("admin.roles")]
    public async Task<ActionResult<ApiResponse<bool>>> AssignRole(int id, [FromBody] AssignRoleRequest request)
    {
        var result = await _mediator.Send(new AssignRoleCommand { UserId = id, RoleId = request.RoleId });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{id:int}/deactivate")]
    [RequirePermission("admin.users")]
    public async Task<ActionResult<ApiResponse<bool>>> Deactivate(int id)
    {
        var result = await _mediator.Send(new DeactivateUserCommand { UserId = id });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Roles ──────────────────────────────────────────────
    [HttpGet("roles")]
    [RequirePermission("admin.roles")]
    public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetRoles()
    {
        var result = await _mediator.Send(new GetRolesQuery());
        return Ok(result);
    }

    [HttpPost("roles")]
    [RequirePermission("admin.roles")]
    public async Task<ActionResult<ApiResponse<RoleDto>>> CreateRole([FromBody] CreateRoleCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("roles/{id:int}/permissions")]
    [RequirePermission("admin.roles")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateRolePermissions(int id, [FromBody] UpdateRolePermissionsCommand command)
    {
        command = command with { RoleId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Document Types ──────────────────────────────────────
    [HttpGet("document-types")]
    public async Task<ActionResult<ApiResponse<List<DocumentTypeDto>>>> GetDocumentTypes()
    {
        var result = await _mediator.Send(new GetDocumentTypesQuery());
        return Ok(result);
    }

    [HttpPost("document-types")]
    [RequirePermission("admin.doctypes")]
    public async Task<ActionResult<ApiResponse<DocumentTypeDto>>> CreateDocumentType([FromBody] CreateDocumentTypeCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("document-types/{id:int}")]
    [RequirePermission("admin.doctypes")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateDocumentType(int id, [FromBody] UpdateDocumentTypeCommand command)
    {
        command = command with { TypeId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Metadata Fields ──────────────────────────────────────
    [HttpGet("metadata-fields")]
    public async Task<ActionResult<ApiResponse<List<MetadataFieldDto>>>> GetMetadataFields([FromQuery] int? documentTypeId = null)
    {
        var result = await _mediator.Send(new GetMetadataFieldsQuery { DocumentTypeId = documentTypeId });
        return Ok(result);
    }

    [HttpPost("metadata-fields")]
    [RequirePermission("admin.metadata")]
    public async Task<ActionResult<ApiResponse<MetadataFieldDto>>> CreateMetadataField([FromBody] CreateMetadataFieldCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Retention Policies ──────────────────────────────────
    [HttpGet("retention-policies")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<List<RetentionPolicyDto>>>> GetRetentionPolicies()
    {
        var result = await _mediator.Send(new GetRetentionPoliciesQuery());
        return Ok(result);
    }

    [HttpPost("retention-policies")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<RetentionPolicyDto>>> CreateRetentionPolicy([FromBody] CreateRetentionPolicyCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── System Settings ──────────────────────────────────────
    [HttpGet("settings")]
    [RequirePermission("admin.system")]
    public async Task<ActionResult<ApiResponse<List<SystemSettingDto>>>> GetSettings([FromQuery] string? group = null)
    {
        var result = await _mediator.Send(new GetSystemSettingsQuery { GroupName = group });
        return Ok(result);
    }

    [HttpPut("settings/{key}")]
    [RequirePermission("admin.system")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
    {
        var result = await _mediator.Send(new UpdateSystemSettingCommand { SettingKey = key, Value = request.Value });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Lookups ──────────────────────────────────────────────
    [HttpGet("lookups/{categoryCode}")]
    public async Task<ActionResult<ApiResponse<List<LookupValueDto>>>> GetLookupValues(string categoryCode)
    {
        var result = await _mediator.Send(new GetLookupValuesQuery { CategoryCode = categoryCode });
        return Ok(result);
    }
}
