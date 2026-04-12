using Microsoft.AspNetCore.Authorization;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.xECM.Application.Commands;
using Darah.ECM.xECM.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.xECM.API.Controllers;

[ApiController, Route("api/v1/workspaces"), Authorize, Produces("application/json")]
public sealed class WorkspacesController : ControllerBase
{
    private readonly IMediator _mediator;
    public WorkspacesController(IMediator m) => _mediator = m;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceListItemDto>>>> List(
        [FromQuery] int? typeId, [FromQuery] string? status, [FromQuery] int? ownerId,
        [FromQuery] string? externalSystem, [FromQuery] bool? isLegalHold, [FromQuery] string? search,
        [FromQuery] string sortBy = "CreatedAt", [FromQuery] string sortDir = "DESC",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new ListWorkspacesQuery(typeId, status, ownerId, null, externalSystem,
            isLegalHold, null, null, search, sortBy, sortDir, page, Math.Min(pageSize, 100)), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> GetById(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new GetWorkspaceByIdQuery(id), ct); return r.Data is null ? NotFound(r) : Ok(r); }

    [HttpGet("by-external")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> GetByExternal(
        [FromQuery] string systemCode, [FromQuery] string objectId, CancellationToken ct)
    { var r = await _mediator.Send(new GetWorkspaceByExternalObjectQuery(systemCode, objectId), ct); return r.Data is null ? NotFound(r) : Ok(r); }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> Create([FromBody] CreateWorkspaceCommand cmd, CancellationToken ct)
    { var r = await _mediator.Send(cmd, ct); return r.Success ? CreatedAtAction(nameof(GetById), new { id = r.Data!.WorkspaceId }, r) : BadRequest(r); }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> Update(Guid id, [FromBody] UpdateWorkspaceCommand cmd, CancellationToken ct)
    { var r = await _mediator.Send(cmd with { WorkspaceId = id }, ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<ApiResponse<bool>>> Activate(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new ActivateWorkspaceCommand(id), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<ApiResponse<bool>>> Close(Guid id, [FromBody] string? reason, CancellationToken ct)
    { var r = await _mediator.Send(new CloseWorkspaceCommand(id, reason), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<ApiResponse<bool>>> Archive(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new ArchiveWorkspaceCommand(id), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpPost("{id:guid}/dispose")]
    public async Task<ActionResult<ApiResponse<bool>>> Dispose(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new DisposeWorkspaceCommand(id), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpPost("{id:guid}/legal-hold")]
    public async Task<ActionResult<ApiResponse<LegalHoldResultDto>>> ApplyLegalHold(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new ApplyWorkspaceLegalHoldCommand(id), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpDelete("{id:guid}/legal-hold")]
    public async Task<ActionResult<ApiResponse<bool>>> ReleaseLegalHold(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new ReleaseWorkspaceLegalHoldCommand(id), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceDocumentDto>>>> GetDocuments(
        Guid id, [FromQuery] string? bindingType, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetWorkspaceDocumentsQuery(id, bindingType, null, Page: page, PageSize: pageSize), ct));

    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<ApiResponse<WorkspaceDocumentDto>>> AddDocument(
        Guid id, [FromBody] AddDocumentRequest req, CancellationToken ct)
    { var r = await _mediator.Send(new BindDocumentToWorkspaceCommand(id, req.DocumentId, req.BindingType, req.Note), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpDelete("{id:guid}/documents/{docId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveDocument(Guid id, Guid docId, CancellationToken ct)
    { var r = await _mediator.Send(new RemoveDocumentFromWorkspaceCommand(id, docId), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpGet("{id:guid}/metadata")]
    public async Task<ActionResult<ApiResponse<List<WorkspaceMetadataValueDto>>>> GetMetadata(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWorkspaceMetadataQuery(id), ct));

    [HttpPost("{id:guid}/external-binding")]
    public async Task<ActionResult<ApiResponse<bool>>> BindExternal(Guid id, [FromBody] BindExternalObjectCommand cmd, CancellationToken ct)
    { var r = await _mediator.Send(cmd with { WorkspaceId = id }, ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpDelete("{id:guid}/external-binding")]
    public async Task<ActionResult<ApiResponse<bool>>> UnbindExternal(Guid id, CancellationToken ct)
    { var r = await _mediator.Send(new UnbindExternalObjectCommand(id), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpPost("{id:guid}/sync")]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> Sync(Guid id, [FromQuery] string direction = "Inbound", CancellationToken ct = default)
    { var r = await _mediator.Send(new TriggerWorkspaceSyncCommand(id, direction), ct); return Ok(r); }

    [HttpGet("{id:guid}/sync/history")]
    public async Task<ActionResult<ApiResponse<PagedResult<SyncEventLogDto>>>> SyncHistory(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetWorkspaceSyncHistoryQuery(id, page, pageSize), ct));

    [HttpGet("{id:guid}/sync/conflicts")]
    public async Task<ActionResult<ApiResponse<List<SyncConflictDto>>>> Conflicts(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSyncConflictsQuery(id), ct));

    [HttpPost("{id:guid}/sync/conflicts/{fieldId:int}/resolve")]
    public async Task<ActionResult<ApiResponse<bool>>> ResolveConflict(Guid id, int fieldId, [FromBody] string resolution, CancellationToken ct)
    { var r = await _mediator.Send(new ResolveConflictCommand(id, fieldId, resolution), ct); return r.Success ? Ok(r) : BadRequest(r); }

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceAuditLogDto>>>> AuditLog(
        Guid id, [FromQuery] string? eventType, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetWorkspaceAuditLogQuery(id, eventType, from, to, page, pageSize), ct));
}

[ApiController, Route("api/v1/admin/external-systems"), Authorize, Produces("application/json")]
public sealed class ExternalSystemsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ExternalSystemsController(IMediator m) => _mediator = m;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ExternalSystemDto>>>> List([FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _mediator.Send(new ListExternalSystemsQuery(isActive), ct));

    [HttpGet("{systemId:int}/mappings")]
    public async Task<ActionResult<ApiResponse<List<SyncMappingDto>>>> GetMappings(int systemId, [FromQuery] string? objectType, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSyncMappingsQuery(systemId, objectType), ct));
}

public sealed record AddDocumentRequest(Guid DocumentId, string BindingType = "Primary", string? Note = null);
