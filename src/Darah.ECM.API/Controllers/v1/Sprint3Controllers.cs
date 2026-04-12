using Darah.ECM.API.Filters;
using Darah.ECM.Application.Common.Models;


using Darah.ECM.Application.Notifications;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Application.Records.Commands;

using Darah.ECM.Application.Audit.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.API.Controllers.v1;

// ─── RECORDS CONTROLLER ───────────────────────────────────────────────────────
[ApiController]
[Route("api/v1/records")]
[Authorize]
[Produces("application/json")]
public sealed class RecordsController : ControllerBase
{
    private readonly IMediator _mediator;
    public RecordsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("{documentId:guid}/declare")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<RecordDeclarationDto>>> DeclareRecord(
        Guid documentId, [FromBody] DeclareRecordRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeclareRecordCommand(
            documentId, req.RecordClassId, req.RetentionPolicyId, req.Note), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("legal-holds/{holdId:int}/apply")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<LegalHoldResultDto>>> ApplyLegalHold(
        int holdId, [FromBody] ApplyLegalHoldRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ApplyLegalHoldCommand(holdId, req.DocumentIds, req.Note), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("disposal-requests")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<DisposalRequestDto>>> CreateDisposal(
        [FromBody] CreateDisposalRequestCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return result.Success ? Created("", result) : BadRequest(result);
    }
}

// ─── METADATA CONTROLLER ─────────────────────────────────────────────────────
[ApiController]
[Route("api/v1/metadata")]
[Authorize]
[Produces("application/json")]
public sealed class MetadataController : ControllerBase
{
    private readonly IMediator _mediator;
    public MetadataController(IMediator mediator) => _mediator = mediator;

    [HttpGet("fields")]
    [RequirePermission("admin.metadata")]
    public async Task<ActionResult<ApiResponse<List<MetadataFieldDto>>>> GetFields(
        [FromQuery] int? documentTypeId, CancellationToken ct)
        => Ok(ApiResponse<List<MetadataFieldDto>>.Ok(new List<MetadataFieldDto>()));

    [HttpPost("fields")]
    [RequirePermission("admin.metadata")]
    public async Task<ActionResult<ApiResponse<MetadataFieldDto>>> CreateField(
        [FromBody] CreateMetadataFieldCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("documents/{documentId:guid}/values")]
    [RequirePermission("documents.update")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateDocumentMetadata(
        Guid documentId, [FromBody] Dictionary<int, string?> fieldValues, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateDocumentMetadataCommand(documentId, fieldValues), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── FOLDERS CONTROLLER ───────────────────────────────────────────────────────
[ApiController]
[Route("api/v1/folders")]
[Authorize]
[Produces("application/json")]
public sealed class FoldersController : ControllerBase
{
    private readonly IMediator _mediator;
    public FoldersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{libraryId:int}/tree")]
    [RequirePermission("documents.read")]
    public async Task<ActionResult<ApiResponse<List<FolderDto>>>> GetTree(
        int libraryId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFolderTreeQuery(libraryId), ct));

    [HttpPost]
    [RequirePermission("documents.create")]
    public async Task<ActionResult<ApiResponse<FolderDto>>> Create(
        [FromBody] CreateFolderCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{folderId:int}/move")]
    [RequirePermission("documents.update")]
    public async Task<ActionResult<ApiResponse<bool>>> Move(
        int folderId, [FromBody] MoveFolderRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new MoveFolderCommand(folderId, req.NewParentFolderId, req.NewLibraryId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── NOTIFICATIONS CONTROLLER ─────────────────────────────────────────────────
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> GetAll(
        [FromQuery] bool? unreadOnly, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetNotificationsQuery(unreadOnly, page, pageSize), ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> UnreadCount(CancellationToken ct)
        => Ok(await _mediator.Send(new GetUnreadCountQuery(), ct));

    [HttpPost("{notificationId:long}/read")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkRead(
        long notificationId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new MarkNotificationReadCommand(notificationId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkAllRead(CancellationToken ct)
    {
        var result = await _mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── AUDIT CONTROLLER ─────────────────────────────────────────────────────────
[ApiController]
[Route("api/v1/audit")]
[Authorize]
[Produces("application/json")]
public sealed class AuditController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuditController(IMediator mediator) => _mediator = mediator;

    [HttpGet("logs")]
    [RequirePermission("audit.read")]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> GetLogs(
        [FromQuery] string? eventType, [FromQuery] string? entityType,
        [FromQuery] string? entityId, [FromQuery] int? userId,
        [FromQuery] string? severity, [FromQuery] bool? isSuccessful,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string sortDir = "DESC",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetAuditLogsQuery(
            eventType, entityType, entityId, userId, severity, isSuccessful,
            dateFrom, dateTo, sortDir, page, Math.Min(pageSize, 200)), ct));

    [HttpGet("summary")]
    [RequirePermission("audit.read")]
    public async Task<ActionResult<ApiResponse<AuditSummaryDto>>> Summary(
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetAuditSummaryQuery(dateFrom, dateTo), ct));

    [HttpGet("export")]
    [RequirePermission("audit.export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? eventType, [FromQuery] string? entityType,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string format = "Excel", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ExportAuditLogsQuery(
            eventType, entityType, null, null, dateFrom, dateTo, format), ct);
        if (!result.Success) return BadRequest(result.Message);

        var contentType = format == "Pdf" ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return File(result.Data!.FileBytes, contentType, result.Data.FileName);
    }
}

// ─── REQUEST MODELS ───────────────────────────────────────────────────────────
public sealed record DeclareRecordRequest(int RecordClassId, int RetentionPolicyId, string? Note);
public sealed record ApplyLegalHoldRequest(Guid[] DocumentIds, string? Note);
public sealed record MoveFolderRequest(int? NewParentFolderId, int NewLibraryId);

// Folder DTO referenced in FoldersController
public sealed record FolderDto(
    int FolderId, string NameAr, string? NameEn, int LibraryId, int? ParentFolderId,
    string Path, int DepthLevel, int SortOrder, DateTime CreatedAt,
    List<FolderDto>? Children = null, int DocumentCount = 0);
