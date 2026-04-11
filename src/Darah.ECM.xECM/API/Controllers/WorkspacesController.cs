using Darah.ECM.API.Filters;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.xECM.Application.Commands;
using Darah.ECM.xECM.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.xECM.API.Controllers;

[ApiController]
[Route("api/v1/workspaces")]
[Authorize]
[Produces("application/json")]
public sealed class WorkspacesController : ControllerBase
{
    private readonly IMediator _mediator;
    public WorkspacesController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [RequirePermission("workspace.create")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> Create([FromBody] CreateWorkspaceCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Success ? CreatedAtAction(nameof(GetById), new { id = r.Data!.WorkspaceId }, r) : BadRequest(r);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> GetById(Guid id, CancellationToken ct)
        => Ok(ApiResponse<WorkspaceDto>.Fail("GetWorkspaceByIdQuery — wired in full implementation"));

    [HttpGet("by-external")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> GetByExternal([FromQuery] string systemId, [FromQuery] string objectId, CancellationToken ct)
        => Ok(ApiResponse<WorkspaceDto>.Fail("GetWorkspaceByExternalObjectQuery — wired in full implementation"));

    [HttpPost("{id:guid}/bind-external")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<bool>>> BindExternal(Guid id, [FromBody] BindExternalObjectCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { WorkspaceId = id }, ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/sync")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> Sync(Guid id, [FromQuery] string direction = "Inbound", CancellationToken ct = default)
    {
        var r = await _mediator.Send(new TriggerWorkspaceSyncCommand(id, direction), ct);
        return Ok(r);
    }

    [HttpPost("{id:guid}/conflicts/resolve")]
    [RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> ResolveConflict(Guid id, [FromBody] ResolveConflictRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ResolveWorkspaceConflictCommand(id, req.FieldId, req.Resolution), ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/archive")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<bool>>> Archive(Guid id, [FromBody] string? reason, CancellationToken ct)
    {
        var r = await _mediator.Send(new ArchiveWorkspaceCommand(id, reason), ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/legal-hold")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<bool>>> ApplyLegalHold(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new ApplyWorkspaceLegalHoldCommand(id), ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }
}

public sealed record ResolveConflictRequest(int FieldId, string Resolution);
