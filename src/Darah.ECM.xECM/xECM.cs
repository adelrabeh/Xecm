// ============================================================
// xECM DOMAIN
// ============================================================
namespace Darah.ECM.xECM.Domain.Entities;

using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Events;

public class Workspace : BaseEntity
{
    public Guid WorkspaceId { get; private set; }
    public string WorkspaceNumber { get; private set; } = string.Empty;
    public int WorkspaceTypeId { get; private set; }
    public string TitleAr { get; private set; } = string.Empty;
    public string? TitleEn { get; private set; }
    public string? Description { get; private set; }
    public int OwnerId { get; private set; }
    public int? DepartmentId { get; private set; }
    // External binding
    public string? ExternalSystemId { get; private set; }
    public string? ExternalObjectId { get; private set; }
    public string? ExternalObjectType { get; private set; }
    public string? ExternalObjectUrl { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public string? SyncStatus { get; private set; }
    public string? SyncError { get; private set; }
    // Lifecycle
    public int StatusValueId { get; private set; }
    public int ClassificationLevelId { get; private set; } = 1;
    public bool IsLegalHold { get; private set; }
    public int? RetentionPolicyId { get; private set; }
    public DateOnly? RetentionExpiresAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public int? ArchivedBy { get; private set; }

    public bool IsBoundToExternal =>
        !string.IsNullOrEmpty(ExternalSystemId) && !string.IsNullOrEmpty(ExternalObjectId);

    private Workspace() { }

    public static Workspace Create(string titleAr, int typeId, int ownerId,
        int statusValueId, string number, int createdBy,
        string? titleEn = null, int? deptId = null, string? description = null,
        int classificationLevelId = 1, int? retentionPolicyId = null)
    {
        var ws = new Workspace
        {
            WorkspaceId = Guid.NewGuid(),
            WorkspaceNumber = number,
            TitleAr = titleAr, TitleEn = titleEn,
            WorkspaceTypeId = typeId, OwnerId = ownerId,
            DepartmentId = deptId, Description = description,
            StatusValueId = statusValueId,
            ClassificationLevelId = classificationLevelId,
            RetentionPolicyId = retentionPolicyId
        };
        ws.SetCreated(createdBy);
        ws.RaiseDomainEvent(new WorkspaceCreatedEvent(ws.WorkspaceId, number, string.Empty, createdBy));
        return ws;
    }

    public void BindToExternal(string systemId, string objectId, string objectType,
        string? objectUrl, int userId)
    {
        ExternalSystemId = systemId; ExternalObjectId = objectId;
        ExternalObjectType = objectType; ExternalObjectUrl = objectUrl;
        SyncStatus = "Pending";
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceLinkedToExternalEvent(
            WorkspaceId, systemId, objectId, objectType, userId));
    }

    public void RecordSyncSuccess(DateTime at, int userId)
    {
        LastSyncedAt = at; SyncStatus = "Synced"; SyncError = null;
        SetUpdated(userId);
    }

    public void RecordSyncFailure(string error, int userId)
    {
        SyncStatus = "Failed"; SyncError = error;
        SetUpdated(userId);
    }

    public void RecordSyncConflict() => SyncStatus = "Conflict";

    public void Archive(int userId)
    {
        ArchivedAt = DateTime.UtcNow; ArchivedBy = userId;
        SetUpdated(userId);
        RaiseDomainEvent(new WorkspaceArchivedEvent(WorkspaceId, userId));
    }

    public void ApplyLegalHold(int userId)
    {
        IsLegalHold = true;
        RaiseDomainEvent(new WorkspaceLegalHoldAppliedEvent(WorkspaceId, userId, 0));
    }

    public void ReleaseLegalHold() => IsLegalHold = false;
    public void UpdateTitle(string titleAr, string? titleEn, int userId) { TitleAr = titleAr; TitleEn = titleEn; SetUpdated(userId); }
}

// ============================================================
// xECM APPLICATION — Commands, Queries, Handlers, DTOs
// ============================================================
namespace Darah.ECM.xECM.Application.DTOs;

public record WorkspaceDto(
    Guid WorkspaceId, string WorkspaceNumber, string TitleAr, string? TitleEn,
    string? TypeCode, string? TypeNameAr, string? StatusAr, string? ClassificationAr,
    string? OwnerNameAr, string? DepartmentAr,
    bool IsBoundToExternal, string? ExternalSystemId, string? ExternalObjectId,
    string? ExternalObjectType, string? ExternalObjectUrl,
    string? SyncStatus, DateTime? LastSyncedAt,
    bool IsLegalHold, DateOnly? RetentionExpiresAt,
    int DocumentCount, DateTime CreatedAt, DateTime? UpdatedAt);

public record WorkspaceListItemDto(
    Guid WorkspaceId, string WorkspaceNumber, string TitleAr, string? TitleEn,
    string? TypeCode, string? TypeNameAr, string? StatusAr,
    string? ExternalSystemId, string? ExternalObjectId, string? SyncStatus,
    bool IsLegalHold, int DocumentCount, DateTime CreatedAt);

public record SyncResultDto(bool IsSuccess, int FieldsUpdated, int ConflictsDetected,
    string? ErrorMessage, long DurationMs);

public record MetadataSyncMappingDto(
    int MappingId, string ExternalObjectType, string ExternalFieldName,
    string ExternalFieldType, int InternalFieldId, string? InternalFieldCode,
    string? InternalFieldLabelAr, string SyncDirection,
    string? TransformExpression, string ConflictStrategy, bool IsActive);

namespace Darah.ECM.xECM.Application.Commands;

using MediatR;
using FluentValidation;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.xECM.Application.DTOs;

public record CreateWorkspaceCommand(
    string TitleAr, string? TitleEn, int WorkspaceTypeId, int OwnerId,
    int? DepartmentId, string? Description, int ClassificationLevelId = 1,
    int? RetentionPolicyId = null, string? ExternalSystemId = null,
    string? ExternalObjectId = null, string? ExternalObjectType = null,
    string? ExternalObjectUrl = null,
    Dictionary<int, string>? MetadataValues = null) : IRequest<ApiResponse<WorkspaceDto>>;

public class CreateWorkspaceValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceValidator()
    {
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(500).WithMessage("عنوان مساحة العمل مطلوب");
        RuleFor(x => x.WorkspaceTypeId).GreaterThan(0).WithMessage("يجب تحديد نوع مساحة العمل");
        RuleFor(x => x.OwnerId).GreaterThan(0).WithMessage("يجب تحديد مالك مساحة العمل");
        RuleFor(x => x.ClassificationLevelId).InclusiveBetween(1, 4);
        When(x => !string.IsNullOrEmpty(x.ExternalSystemId), () =>
        {
            RuleFor(x => x.ExternalObjectId).NotEmpty().WithMessage("معرف الكيان الخارجي مطلوب");
            RuleFor(x => x.ExternalObjectType).NotEmpty().WithMessage("نوع الكيان الخارجي مطلوب");
        });
    }
}

public record BindExternalObjectCommand(
    Guid WorkspaceId, string ExternalSystemId, string ExternalObjectId,
    string ExternalObjectType, string? ExternalObjectUrl,
    bool TriggerImmediateSync = true) : IRequest<ApiResponse<bool>>;

public record TriggerSyncCommand(
    Guid WorkspaceId, string Direction = "Inbound") : IRequest<ApiResponse<SyncResultDto>>;

public record ResolveConflictCommand(
    Guid WorkspaceId, int FieldId, string Resolution) : IRequest<ApiResponse<bool>>;

public record AddDocumentToWorkspaceCommand(
    Guid WorkspaceId, Guid DocumentId,
    string BindingType = "Primary") : IRequest<ApiResponse<bool>>;

public record ApplyWorkspaceLegalHoldCommand(Guid WorkspaceId, int HoldId) : IRequest<ApiResponse<bool>>;
public record ReleaseWorkspaceLegalHoldCommand(Guid WorkspaceId) : IRequest<ApiResponse<bool>>;
public record ArchiveWorkspaceCommand(Guid WorkspaceId, string? Reason) : IRequest<ApiResponse<bool>>;

namespace Darah.ECM.xECM.Application.Queries;

using MediatR;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.xECM.Application.DTOs;

public record GetWorkspacesQuery(
    int? WorkspaceTypeId, int? StatusValueId, int? DepartmentId,
    string? ExternalSystemId, string? ExternalObjectId,
    string? TextSearch, bool? IsLegalHold, string? SyncStatus,
    string SortBy = "CreatedAt", string SortDirection = "DESC",
    int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<WorkspaceListItemDto>>>;

public record GetWorkspaceByIdQuery(Guid WorkspaceId) : IRequest<ApiResponse<WorkspaceDto>>;

public record GetWorkspaceByExternalObjectQuery(
    string ExternalSystemId, string ExternalObjectId) : IRequest<ApiResponse<WorkspaceDto>>;

public record GetWorkspaceSyncHistoryQuery(
    Guid WorkspaceId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<SyncEventLogDto>>>;

public record SyncEventLogDto(
    long LogId, string EventType, string Direction,
    string? ExternalObjectId, bool IsSuccessful,
    string? ErrorMessage, long? DurationMs, DateTime CreatedAt);

public record GetSyncMappingsQuery(int ExternalSystemId)
    : IRequest<ApiResponse<List<MetadataSyncMappingDto>>>;

// ============================================================
// xECM INFRASTRUCTURE — Sync Engine + Connectors
// ============================================================
namespace Darah.ECM.xECM.Infrastructure.Sync;

using Darah.ECM.Application.EventHandlers;
using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

public class MetadataSyncEngine : IMetadataSyncEngine
{
    private readonly IEnumerable<IExternalSystemConnector> _connectors;
    private readonly ILogger<MetadataSyncEngine> _logger;

    public MetadataSyncEngine(IEnumerable<IExternalSystemConnector> connectors,
        ILogger<MetadataSyncEngine> logger)
        { _connectors = connectors; _logger = logger; }

    public async Task TriggerSyncAsync(Guid workspaceId, SyncDirection direction, CancellationToken ct = default)
    {
        _logger.LogInformation("Triggering {Direction} sync for workspace {Id}", direction, workspaceId);
        // Full implementation: load workspace, resolve connector, apply field mappings
        await Task.CompletedTask;
    }

    public async Task BulkSyncAsync(string systemCode, CancellationToken ct = default)
    {
        _logger.LogInformation("Bulk sync for system {System}", systemCode);
        await Task.CompletedTask;
    }
}

namespace Darah.ECM.xECM.Infrastructure.Connectors;

using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class SAPConnector : IExternalSystemConnector
{
    public string SystemCode => "SAP_PROD";
    private readonly HttpClient _http;
    private readonly ILogger<SAPConnector> _logger;

    public SAPConnector(HttpClient http, ILogger<SAPConnector> logger)
        { _http = http; _logger = logger; }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try { var r = await _http.GetAsync("/sap/opu/odata/sap/", ct); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default)
    {
        var url = BuildUrl(objectType, objectId);
        if (url is null) return null;
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync(ct);
        var root = System.Text.Json.JsonDocument.Parse(json).RootElement;
        var fields = new Dictionary<string, object?>();
        if (root.TryGetProperty("d", out var d))
            foreach (var p in d.EnumerateObject())
                fields[p.Name] = p.Value.ToString();
        return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId,
        Dictionary<string, object> fields, CancellationToken ct = default)
    {
        var url = BuildUrl(objectType, objectId);
        if (url is null) return false;
        var json = System.Text.Json.JsonSerializer.Serialize(fields);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var resp = await _http.PatchAsync(url, content, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(
        string objectType, DateTime since, CancellationToken ct = default)
        => Enumerable.Empty<ExternalObjectPayload>();

    private static string? BuildUrl(string objectType, string objectId) => objectType switch
    {
        "WBSElement"    => $"/sap/opu/odata/sap/API_PROJECT/A_EnterpriseProjectElement('{objectId}')",
        "PurchaseOrder" => $"/sap/opu/odata/sap/API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder('{objectId}')",
        "Contract"      => $"/sap/opu/odata/sap/API_CONTRACT/A_Contract('{objectId}')",
        _ => null
    };
}

public class SalesforceConnector : IExternalSystemConnector
{
    public string SystemCode => "SF_CRM";
    private readonly HttpClient _http;
    private readonly ILogger<SalesforceConnector> _logger;

    public SalesforceConnector(HttpClient http, ILogger<SalesforceConnector> logger)
        { _http = http; _logger = logger; }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try { var r = await _http.GetAsync("/services/data/v58.0/", ct); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<ExternalObjectPayload?> FetchObjectAsync(string objectType, string objectId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"/services/data/v58.0/sobjects/{objectType}/{objectId}", ct);
        if (!r.IsSuccessStatusCode) return null;
        var json = await r.Content.ReadAsStringAsync(ct);
        var root = System.Text.Json.JsonDocument.Parse(json).RootElement;
        var fields = new Dictionary<string, object?>();
        foreach (var p in root.EnumerateObject())
            if (p.Name != "attributes") fields[p.Name] = p.Value.ToString();
        return new ExternalObjectPayload(objectId, objectType, fields, DateTime.UtcNow);
    }

    public async Task<bool> PushUpdateAsync(string objectType, string objectId,
        Dictionary<string, object> fields, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(fields);
        var req = new HttpRequestMessage(HttpMethod.Patch,
            $"/services/data/v58.0/sobjects/{objectType}/{objectId}")
            { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        var r = await _http.SendAsync(req, ct);
        return r.IsSuccessStatusCode;
    }

    public Task<IEnumerable<ExternalObjectPayload>> FetchChangedSinceAsync(
        string objectType, DateTime since, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<ExternalObjectPayload>());
}

// ============================================================
// xECM API — WorkspacesController + ExternalSystemsController
// ============================================================
namespace Darah.ECM.xECM.API.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Darah.ECM.API.Filters;
using Darah.ECM.API.Models.Requests;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.xECM.Application.Commands;
using Darah.ECM.xECM.Application.Queries;
using Darah.ECM.xECM.Application.DTOs;

[ApiController, Authorize]
[Route("api/v1/workspaces")]
public class WorkspacesController : ControllerBase
{
    private readonly IMediator _mediator;
    public WorkspacesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceListItemDto>>>> GetAll(
        [FromQuery] int? typeId, [FromQuery] int? statusValueId,
        [FromQuery] int? departmentId, [FromQuery] string? externalSystemId,
        [FromQuery] string? externalObjectId, [FromQuery] string? search,
        [FromQuery] bool? isLegalHold, [FromQuery] string? syncStatus,
        [FromQuery] string sortBy = "CreatedAt", [FromQuery] string sortDir = "DESC",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetWorkspacesQuery(typeId, statusValueId, departmentId,
            externalSystemId, externalObjectId, search, isLegalHold, syncStatus,
            sortBy, sortDir, page, Math.Min(pageSize, 100))));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> Get(Guid id)
    {
        var r = await _mediator.Send(new GetWorkspaceByIdQuery(id));
        return r.Data is null ? NotFound(ApiResponse<WorkspaceDto>.Fail("مساحة العمل غير موجودة")) : Ok(r);
    }

    [HttpGet("by-external")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> GetByExternal(
        [FromQuery] string systemId, [FromQuery] string objectId)
    {
        var r = await _mediator.Send(new GetWorkspaceByExternalObjectQuery(systemId, objectId));
        return r.Data is null ? NotFound(ApiResponse<WorkspaceDto>.Fail("لا توجد مساحة عمل لهذا الكيان")) : Ok(r);
    }

    [HttpPost, RequirePermission("workspace.create")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> Create([FromBody] CreateWorkspaceCommand cmd)
    {
        var r = await _mediator.Send(cmd);
        return r.Success ? CreatedAtAction(nameof(Get), new { id = r.Data!.WorkspaceId }, r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/bind-external"), RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<bool>>> BindExternal(Guid id, [FromBody] BindExternalObjectCommand cmd)
    {
        var r = await _mediator.Send(cmd with { WorkspaceId = id });
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/sync"), RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> Sync(Guid id,
        [FromQuery] string direction = "Inbound")
    {
        var r = await _mediator.Send(new TriggerSyncCommand(id, direction));
        return Ok(r);
    }

    [HttpGet("{id:guid}/sync-history")]
    public async Task<ActionResult<ApiResponse<PagedResult<SyncEventLogDto>>>> SyncHistory(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetWorkspaceSyncHistoryQuery(id, page, pageSize)));

    [HttpPost("{id:guid}/conflicts/resolve"), RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> ResolveConflict(Guid id,
        [FromBody] ResolveConflictRequest req)
    {
        var r = await _mediator.Send(new ResolveConflictCommand(id, req.FieldId, req.Resolution));
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/documents"), RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> AddDocument(Guid id,
        [FromBody] AddDocumentToWorkspaceCommand cmd)
    {
        var r = await _mediator.Send(cmd with { WorkspaceId = id });
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/legal-hold"), RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<bool>>> ApplyLegalHold(Guid id,
        [FromBody] ApplyWorkspaceLegalHoldCommand cmd)
    {
        var r = await _mediator.Send(cmd with { WorkspaceId = id });
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpDelete("{id:guid}/legal-hold"), RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<bool>>> ReleaseLegalHold(Guid id)
    {
        var r = await _mediator.Send(new ReleaseWorkspaceLegalHoldCommand(id));
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPost("{id:guid}/archive"), RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<bool>>> Archive(Guid id, [FromBody] ArchiveWorkspaceCommand cmd)
    {
        var r = await _mediator.Send(cmd with { WorkspaceId = id });
        return r.Success ? Ok(r) : BadRequest(r);
    }
}

[ApiController, Authorize]
[Route("api/v1/admin/external-systems")]
[RequirePermission("admin.system")]
public class ExternalSystemsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ExternalSystemsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:int}/mappings")]
    public async Task<ActionResult<ApiResponse<List<MetadataSyncMappingDto>>>> GetMappings(int id)
        => Ok(await _mediator.Send(new GetSyncMappingsQuery(id)));
}
