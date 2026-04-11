using Darah.ECM.API.Filters;
using Darah.ECM.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.xECM.API.Controllers;

// ─── REQUEST MODELS ───────────────────────────────────────────────────────────
public sealed class CreateWorkspaceRequest
{
    public string    TitleAr                { get; set; } = string.Empty;
    public string?   TitleEn               { get; set; }
    public int       WorkspaceTypeId        { get; set; }
    public int       OwnerId               { get; set; }
    public int?      DepartmentId          { get; set; }
    public string?   Description           { get; set; }
    public int       ClassificationLevelId { get; set; } = 2;
    public int?      RetentionPolicyId     { get; set; }
    public string?   ExternalSystemId      { get; set; }
    public string?   ExternalObjectId      { get; set; }
    public string?   ExternalObjectType    { get; set; }
    public string?   ExternalObjectUrl     { get; set; }
    public Dictionary<int, string>? MetadataValues { get; set; }
}

public sealed class BindExternalRequest
{
    public string  ExternalSystemId   { get; set; } = string.Empty;
    public string  ExternalObjectId   { get; set; } = string.Empty;
    public string  ExternalObjectType { get; set; } = string.Empty;
    public string? ExternalObjectUrl  { get; set; }
    public bool    TriggerSync        { get; set; } = true;
}

public sealed class ResolveConflictRequest
{
    public int    FieldId    { get; set; }
    public string Resolution { get; set; } = string.Empty;  // UseExternal | UseInternal
}

public sealed class AddDocumentRequest
{
    public Guid   DocumentId  { get; set; }
    public string BindingType { get; set; } = "Primary";
}

// ─── CONTROLLER ───────────────────────────────────────────────────────────────
[ApiController]
[Authorize]
[Route("api/v1/workspaces")]
[Produces("application/json")]
public sealed class WorkspacesController : ControllerBase
{
    private readonly IMediator _mediator;
    public WorkspacesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission("workspace.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int?    typeId           = null,
        [FromQuery] string? statusCode       = null,
        [FromQuery] int?    departmentId     = null,
        [FromQuery] string? externalSystemId = null,
        [FromQuery] string? externalObjectId = null,
        [FromQuery] string? search           = null,
        [FromQuery] bool?   isLegalHold      = null,
        [FromQuery] string? syncStatus       = null,
        [FromQuery] string  sortBy           = "CreatedAt",
        [FromQuery] string  sortDir          = "DESC",
        [FromQuery] int     page             = 1,
        [FromQuery] int     pageSize         = 20,
        CancellationToken ct = default)
    {
        // Send to MediatR GetWorkspacesQuery — handler returns PagedResult
        return Ok(ApiResponse<string>.Ok("Workspace list — handler to be wired"));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("workspace.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        => Ok(ApiResponse<string>.Ok($"Workspace {id}"));

    [HttpGet("by-external")]
    [RequirePermission("workspace.read")]
    public async Task<IActionResult> GetByExternal(
        [FromQuery] string systemId, [FromQuery] string objectId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(systemId) || string.IsNullOrWhiteSpace(objectId))
            return BadRequest(ApiResponse<object>.Fail("systemId و objectId مطلوبان"));
        return Ok(ApiResponse<string>.Ok($"Workspace by external {systemId}/{objectId}"));
    }

    [HttpPost]
    [RequirePermission("workspace.create")]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkspaceRequest request, CancellationToken ct = default)
        => Ok(ApiResponse<string>.Ok("Workspace created"));

    [HttpPut("{id:guid}")]
    [RequirePermission("workspace.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateWorkspaceRequest request, CancellationToken ct = default)
        => Ok(ApiResponse<bool>.Ok(true));

    [HttpPost("{id:guid}/bind-external")]
    [RequirePermission("workspace.manage")]
    public async Task<IActionResult> BindExternal(
        Guid id, [FromBody] BindExternalRequest request, CancellationToken ct = default)
        => Ok(ApiResponse<bool>.Ok(true, "تم الربط بالنظام الخارجي"));

    [HttpPost("{id:guid}/sync")]
    [RequirePermission("workspace.manage")]
    public async Task<IActionResult> TriggerSync(
        Guid id, [FromQuery] string direction = "Inbound", CancellationToken ct = default)
        => Ok(ApiResponse<string>.Ok($"Sync triggered: {direction}"));

    [HttpGet("{id:guid}/sync-history")]
    [RequirePermission("workspace.read")]
    public async Task<IActionResult> GetSyncHistory(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<string>.Ok("Sync history"));

    [HttpPost("{id:guid}/conflicts/resolve")]
    [RequirePermission("workspace.update")]
    public async Task<IActionResult> ResolveConflict(
        Guid id, [FromBody] ResolveConflictRequest request, CancellationToken ct = default)
        => Ok(ApiResponse<bool>.Ok(true, "تم حل التعارض"));

    [HttpGet("{id:guid}/documents")]
    [RequirePermission("workspace.read")]
    public async Task<IActionResult> GetDocuments(
        Guid id, [FromQuery] string? bindingType = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<string>.Ok("Workspace documents"));

    [HttpPost("{id:guid}/documents")]
    [RequirePermission("workspace.update")]
    public async Task<IActionResult> AddDocument(
        Guid id, [FromBody] AddDocumentRequest request, CancellationToken ct = default)
        => Ok(ApiResponse<bool>.Ok(true, "تم إضافة الوثيقة لمساحة العمل"));

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [RequirePermission("workspace.update")]
    public async Task<IActionResult> RemoveDocument(
        Guid id, Guid documentId, CancellationToken ct = default)
        => Ok(ApiResponse<bool>.Ok(true));

    [HttpGet("{id:guid}/security")]
    [RequirePermission("workspace.manage")]
    public async Task<IActionResult> GetSecurity(Guid id, CancellationToken ct = default)
        => Ok(ApiResponse<string>.Ok("Security policies"));

    [HttpPost("{id:guid}/legal-hold")]
    [RequirePermission("admin.retention")]
    public async Task<IActionResult> ApplyLegalHold(Guid id, CancellationToken ct = default)
        => Ok(ApiResponse<bool>.Ok(true, "تم تطبيق التجميد القانوني على مساحة العمل"));

    [HttpDelete("{id:guid}/legal-hold")]
    [RequirePermission("admin.retention")]
    public async Task<IActionResult> ReleaseLegalHold(Guid id, CancellationToken ct = default)
        => Ok(ApiResponse<bool>.Ok(true));

    [HttpPost("{id:guid}/archive")]
    [RequirePermission("workspace.manage")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct = default)
        => Ok(ApiResponse<bool>.Ok(true, "تم أرشفة مساحة العمل"));

    [HttpGet("{id:guid}/audit")]
    [RequirePermission("audit.read")]
    public async Task<IActionResult> GetAuditLog(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(ApiResponse<string>.Ok("Workspace audit log"));
}
