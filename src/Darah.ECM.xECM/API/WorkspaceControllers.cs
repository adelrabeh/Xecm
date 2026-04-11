// ================================================================
// FILE: src/API/Controllers/WorkspacesController.cs
// DARAH ECM xECM — Workspace API
// ================================================================
namespace Darah.ECM.API.Controllers;

[ApiController]
[Route("api/v1/workspaces")]
[Authorize]
public class WorkspacesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;

    public WorkspacesController(IMediator mediator, ICurrentUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    // ── List & Search ──────────────────────────────────────────

    /// <summary>List workspaces with filtering and pagination</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceListItemDto>>>> GetWorkspaces(
        [FromQuery] int? typeId,
        [FromQuery] int? statusValueId,
        [FromQuery] int? departmentId,
        [FromQuery] string? externalSystemId,
        [FromQuery] string? externalObjectId,
        [FromQuery] string? search,
        [FromQuery] bool? isLegalHold,
        [FromQuery] string? syncStatus,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] string sortDir = "DESC",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetWorkspacesQuery
        {
            WorkspaceTypeId = typeId, StatusValueId = statusValueId,
            DepartmentId = departmentId, ExternalSystemId = externalSystemId,
            ExternalObjectId = externalObjectId, TextSearch = search,
            IsLegalHold = isLegalHold, SyncStatus = syncStatus,
            SortBy = sortBy, SortDirection = sortDir,
            Page = page, PageSize = Math.Min(pageSize, 100)
        });
        return Ok(result);
    }

    /// <summary>Get workspace details by ID</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> GetWorkspace(Guid id)
    {
        var result = await _mediator.Send(new GetWorkspaceByIdQuery { WorkspaceId = id });
        if (result.Data == null) return NotFound(ApiResponse<WorkspaceDto>.Fail("مساحة العمل غير موجودة"));
        return Ok(result);
    }

    /// <summary>Find workspace by external object binding</summary>
    [HttpGet("by-external")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> GetByExternalObject(
        [FromQuery] string systemId,
        [FromQuery] string objectId)
    {
        if (string.IsNullOrWhiteSpace(systemId) || string.IsNullOrWhiteSpace(objectId))
            return BadRequest(ApiResponse<WorkspaceDto>.Fail("systemId و objectId مطلوبان"));

        var result = await _mediator.Send(new GetWorkspaceByExternalObjectQuery
        {
            ExternalSystemId = systemId,
            ExternalObjectId = objectId
        });
        if (result.Data == null) return NotFound(ApiResponse<WorkspaceDto>.Fail("لا توجد مساحة عمل مرتبطة بهذا الكيان الخارجي"));
        return Ok(result);
    }

    // ── CRUD ──────────────────────────────────────────────────

    /// <summary>Create a new workspace</summary>
    [HttpPost]
    [RequirePermission("workspace.create")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> Create([FromBody] CreateWorkspaceCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success) return BadRequest(result);
        return CreatedAtAction(nameof(GetWorkspace), new { id = result.Data!.WorkspaceId }, result);
    }

    /// <summary>Update workspace title, description, owner</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(Guid id, [FromBody] UpdateWorkspaceCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Update workspace metadata values</summary>
    [HttpPut("{id:guid}/metadata")]
    [RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateMetadata(Guid id, [FromBody] UpdateWorkspaceMetadataCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Change workspace status (Active/Closed/Archived)</summary>
    [HttpPost("{id:guid}/status")]
    [RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> ChangeStatus(Guid id, [FromBody] ChangeWorkspaceStatusCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Archive a workspace (cascades to documents)</summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<bool>>> Archive(Guid id, [FromBody] ArchiveWorkspaceCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── External Binding ───────────────────────────────────────

    /// <summary>Bind workspace to an external system object</summary>
    [HttpPost("{id:guid}/bind-external")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<bool>>> BindExternal(Guid id, [FromBody] BindExternalObjectCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Trigger metadata sync from external system</summary>
    [HttpPost("{id:guid}/sync")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> TriggerSync(Guid id, [FromQuery] string direction = "Inbound")
    {
        if (!Enum.TryParse<SyncDirection>(direction, true, out var dir))
            return BadRequest(ApiResponse<SyncResultDto>.Fail("اتجاه المزامنة غير صحيح. استخدم: Inbound, Outbound, Bidirectional"));

        var result = await _mediator.Send(new TriggerWorkspaceSyncCommand { WorkspaceId = id, Direction = dir });
        return Ok(result);
    }

    /// <summary>Get sync history for a workspace</summary>
    [HttpGet("{id:guid}/sync-history")]
    public async Task<ActionResult<ApiResponse<PagedResult<SyncEventLogDto>>>> GetSyncHistory(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetWorkspaceSyncHistoryQuery
        {
            WorkspaceId = id, Page = page, PageSize = pageSize
        });
        return Ok(result);
    }

    /// <summary>Resolve a metadata conflict</summary>
    [HttpPost("{id:guid}/conflicts/resolve")]
    [RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> ResolveConflict(Guid id, [FromBody] ResolveConflictRequest request)
    {
        var result = await _mediator.Send(new ResolveWorkspaceConflictCommand
        {
            WorkspaceId = id,
            FieldId = request.FieldId,
            Resolution = request.Resolution  // UseExternal | UseInternal
        });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Documents ──────────────────────────────────────────────

    /// <summary>List documents in workspace</summary>
    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentDto>>>> GetDocuments(
        Guid id,
        [FromQuery] string? bindingType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetWorkspaceDocumentsQuery
        {
            WorkspaceId = id, BindingType = bindingType, Page = page, PageSize = pageSize
        });
        return Ok(result);
    }

    /// <summary>Add document to workspace</summary>
    [HttpPost("{id:guid}/documents")]
    [RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> AddDocument(Guid id, [FromBody] AddDocumentToWorkspaceCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Remove document from workspace</summary>
    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [RequirePermission("workspace.update")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveDocument(Guid id, Guid documentId)
    {
        var result = await _mediator.Send(new RemoveDocumentFromWorkspaceCommand
        {
            WorkspaceId = id, DocumentId = documentId
        });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Security ───────────────────────────────────────────────

    /// <summary>Get workspace security policies</summary>
    [HttpGet("{id:guid}/security")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<List<WorkspaceSecurityPolicyDto>>>> GetSecurityPolicies(Guid id)
    {
        var result = await _mediator.Send(new GetWorkspaceSecurityPoliciesQuery { WorkspaceId = id });
        return Ok(result);
    }

    /// <summary>Add/update security policy for workspace</summary>
    [HttpPut("{id:guid}/security")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<bool>>> SetSecurityPolicy(Guid id, [FromBody] SetWorkspaceSecurityPolicyCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Propagate workspace security to all its documents</summary>
    [HttpPost("{id:guid}/security/propagate")]
    [RequirePermission("workspace.manage")]
    public async Task<ActionResult<ApiResponse<bool>>> PropagateSecurity(Guid id)
    {
        var result = await _mediator.Send(new PropagateWorkspaceSecurityCommand { WorkspaceId = id });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Legal Hold ──────────────────────────────────────────────

    /// <summary>Apply legal hold to entire workspace (cascades to all documents)</summary>
    [HttpPost("{id:guid}/legal-hold")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<bool>>> ApplyLegalHold(Guid id, [FromBody] ApplyWorkspaceLegalHoldCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Release legal hold from workspace</summary>
    [HttpDelete("{id:guid}/legal-hold")]
    [RequirePermission("admin.retention")]
    public async Task<ActionResult<ApiResponse<bool>>> ReleaseLegalHold(Guid id)
    {
        var result = await _mediator.Send(new ReleaseWorkspaceLegalHoldCommand { WorkspaceId = id });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Workflow ───────────────────────────────────────────────

    /// <summary>Submit workspace for a workflow (workspace-level approval)</summary>
    [HttpPost("{id:guid}/workflow/submit")]
    [RequirePermission("workflow.submit")]
    public async Task<ActionResult<ApiResponse<WorkflowInstanceDto>>> SubmitWorkflow(Guid id, [FromBody] SubmitWorkspaceToWorkflowCommand command)
    {
        command = command with { WorkspaceId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Audit ──────────────────────────────────────────────────

    /// <summary>Get audit log specific to this workspace</summary>
    [HttpGet("{id:guid}/audit")]
    [RequirePermission("audit.read")]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> GetAuditLog(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetWorkspaceAuditLogQuery
        {
            WorkspaceId = id, Page = page, PageSize = pageSize
        });
        return Ok(result);
    }
}

// ================================================================
// FILE: src/API/Controllers/ExternalSystemsController.cs
// ================================================================
[ApiController]
[Route("api/v1/admin/external-systems")]
[Authorize]
[RequirePermission("admin.system")]
public class ExternalSystemsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMetadataSyncEngine _syncEngine;

    public ExternalSystemsController(IMediator mediator, IMetadataSyncEngine syncEngine)
    {
        _mediator = mediator; _syncEngine = syncEngine;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ExternalSystemDto>>>> GetAll()
    {
        var result = await _mediator.Send(new GetExternalSystemsQuery());
        return Ok(result);
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<ApiResponse<ExternalSystemDto>>> GetByCode(string code)
    {
        var result = await _mediator.Send(new GetExternalSystemByCodeQuery { SystemCode = code });
        if (result.Data == null) return NotFound(ApiResponse<ExternalSystemDto>.Fail("النظام الخارجي غير موجود"));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ExternalSystemDto>>> Create([FromBody] CreateExternalSystemCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(int id, [FromBody] UpdateExternalSystemCommand command)
    {
        command = command with { SystemId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Test connectivity to external system</summary>
    [HttpPost("{code}/test-connection")]
    public async Task<ActionResult<ApiResponse<bool>>> TestConnection(string code)
    {
        var result = await _mediator.Send(new TestExternalSystemConnectionCommand { SystemCode = code });
        return Ok(result);
    }

    /// <summary>Trigger bulk sync for all workspaces of this system</summary>
    [HttpPost("{code}/sync")]
    public async Task<ActionResult<ApiResponse<BulkSyncResultDto>>> BulkSync(string code)
    {
        var raw = await _syncEngine.BulkSyncAsync(code);
        return Ok(ApiResponse<BulkSyncResultDto>.Ok(new BulkSyncResultDto
        {
            WorkspacesSynced = raw.WorkspacesSynced,
            WorkspacesFailed = raw.WorkspacesFailed,
            TotalFieldsUpdated = raw.TotalFieldsUpdated
        }, $"تمت مزامنة {raw.WorkspacesSynced} مساحة عمل. فشل: {raw.WorkspacesFailed}"));
    }

    // ── Field Mappings ─────────────────────────────────────────

    [HttpGet("{id:int}/mappings")]
    public async Task<ActionResult<ApiResponse<List<MetadataSyncMappingDto>>>> GetMappings(int id)
    {
        var result = await _mediator.Send(new GetSyncMappingsQuery { ExternalSystemId = id });
        return Ok(result);
    }

    [HttpPost("{id:int}/mappings")]
    public async Task<ActionResult<ApiResponse<MetadataSyncMappingDto>>> CreateMapping(int id, [FromBody] CreateSyncMappingCommand command)
    {
        command = command with { ExternalSystemId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}/mappings/{mappingId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateMapping(int id, int mappingId, [FromBody] UpdateSyncMappingCommand command)
    {
        command = command with { MappingId = mappingId, ExternalSystemId = id };
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}/mappings/{mappingId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteMapping(int id, int mappingId)
    {
        var result = await _mediator.Send(new DeleteSyncMappingCommand { MappingId = mappingId });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Preview what a sync would produce (dry run)</summary>
    [HttpPost("{code}/mappings/preview")]
    public async Task<ActionResult<ApiResponse<SyncPreviewDto>>> PreviewSync(string code, [FromBody] SyncPreviewRequest request)
    {
        var result = await _mediator.Send(new PreviewSyncCommand
        {
            SystemCode = code, ExternalObjectId = request.ExternalObjectId, ExternalObjectType = request.ExternalObjectType
        });
        return Ok(result);
    }
}

// ================================================================
// FILE: src/Infrastructure/Security/WorkspaceSecurityService.cs
// ================================================================
namespace Darah.ECM.Infrastructure.Security;

public class WorkspaceSecurityService : IWorkspaceSecurityService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public WorkspaceSecurityService(IApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context; _currentUser = currentUser;
    }

    public async Task<bool> CanReadWorkspaceAsync(Guid workspaceId, int userId, CancellationToken ct = default)
        => await EvaluateAsync(workspaceId, userId, p => p.CanRead, ct);

    public async Task<bool> CanWriteWorkspaceAsync(Guid workspaceId, int userId, CancellationToken ct = default)
        => await EvaluateAsync(workspaceId, userId, p => p.CanWrite, ct);

    public async Task<bool> CanManageWorkspaceAsync(Guid workspaceId, int userId, CancellationToken ct = default)
        => await EvaluateAsync(workspaceId, userId, p => p.CanManage, ct);

    private async Task<bool> EvaluateAsync(Guid workspaceId, int userId,
        Func<WorkspaceSecurityPolicy, bool> permSelector, CancellationToken ct)
    {
        // SystemAdmin bypasses all workspace-level checks
        if (_currentUser.HasPermission("admin.system")) return true;

        var userRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.RoleId).ToListAsync(ct);

        var userDeptIds = await _context.Users
            .Where(u => u.UserId == userId)
            .Select(u => u.DepartmentId).ToListAsync(ct);

        var policies = await _context.WorkspaceSecurityPolicies
            .Where(p => p.WorkspaceId == workspaceId && (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow))
            .ToListAsync(ct);

        // Check for explicit deny first (deny wins)
        var hasDeny = policies.Any(p => p.IsDeny && permSelector(p) &&
            ((p.PrincipalType == "User" && p.PrincipalId == userId) ||
             (p.PrincipalType == "Role" && userRoleIds.Contains(p.PrincipalId)) ||
             (p.PrincipalType == "Department" && userDeptIds.Contains(p.PrincipalId))));

        if (hasDeny) return false;

        // Check for allow
        return policies.Any(p => !p.IsDeny && permSelector(p) &&
            ((p.PrincipalType == "User" && p.PrincipalId == userId) ||
             (p.PrincipalType == "Role" && userRoleIds.Contains(p.PrincipalId)) ||
             (p.PrincipalType == "Department" && userDeptIds.Contains(p.PrincipalId))));
    }

    public async Task PropagateSecurityToDocumentAsync(Workspace workspace, Guid documentId, CancellationToken ct = default)
    {
        var wsPolicies = await _context.WorkspaceSecurityPolicies
            .Where(p => p.WorkspaceId == workspace.WorkspaceId && p.InheritToDocuments)
            .ToListAsync(ct);

        foreach (var wsPolicy in wsPolicies)
        {
            var exists = await _context.DocumentAccessPermissions.AnyAsync(dap =>
                dap.EntityType == "Document" && dap.EntityId == documentId.ToString() &&
                dap.PrincipalType == wsPolicy.PrincipalType && dap.PrincipalId == wsPolicy.PrincipalId, ct);

            if (!exists)
            {
                _context.DocumentAccessPermissions.Add(new DocumentAccessPermission
                {
                    EntityType = "Document",
                    EntityId = documentId.ToString(),
                    PrincipalType = wsPolicy.PrincipalType,
                    PrincipalId = wsPolicy.PrincipalId,
                    CanRead = wsPolicy.CanRead,
                    CanWrite = wsPolicy.CanWrite,
                    CanDelete = wsPolicy.CanDelete,
                    CanDownload = wsPolicy.CanDownload,
                    CanManage = wsPolicy.CanManage,
                    IsDeny = wsPolicy.IsDeny,
                    GrantedBy = _currentUser.UserId
                });
            }
        }
    }

    public async Task PropagateSecurityToAllDocumentsAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var workspace = await _context.Workspaces.FindAsync(new object[] { workspaceId }, ct);
        if (workspace == null) return;

        var docIds = await _context.WorkspaceDocuments
            .Where(wd => wd.WorkspaceId == workspaceId && wd.IsActive)
            .Select(wd => wd.DocumentId).ToListAsync(ct);

        foreach (var docId in docIds)
            await PropagateSecurityToDocumentAsync(workspace, docId, ct);

        await _context.SaveChangesAsync(ct);
    }
}

// ── DTOs ────────────────────────────────────────────────────────
public class ExternalSystemDto
{
    public int SystemId { get; set; }
    public string SystemCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string AuthType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public string? ConnectionStatus { get; set; }
    public int SyncIntervalMinutes { get; set; }
}

public class MetadataSyncMappingDto
{
    public int MappingId { get; set; }
    public string ExternalObjectType { get; set; } = string.Empty;
    public string ExternalFieldName { get; set; } = string.Empty;
    public string ExternalFieldType { get; set; } = string.Empty;
    public int InternalFieldId { get; set; }
    public string? InternalFieldCode { get; set; }
    public string? InternalFieldLabelAr { get; set; }
    public string SyncDirection { get; set; } = string.Empty;
    public string? TransformExpression { get; set; }
    public string ConflictStrategy { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class SyncResultDto
{
    public bool IsSuccess { get; set; }
    public int FieldsUpdated { get; set; }
    public int ConflictsDetected { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
}

public class BulkSyncResultDto
{
    public int WorkspacesSynced { get; set; }
    public int WorkspacesFailed { get; set; }
    public int TotalFieldsUpdated { get; set; }
}

public class SyncEventLogDto
{
    public long LogId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string? ExternalObjectId { get; set; }
    public int? FieldsUpdated { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public long? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WorkspaceSecurityPolicyDto
{
    public int PolicyId { get; set; }
    public string PrincipalType { get; set; } = string.Empty;
    public int PrincipalId { get; set; }
    public string? PrincipalNameAr { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanDownload { get; set; }
    public bool CanManage { get; set; }
    public bool IsDeny { get; set; }
    public bool InheritToDocuments { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class SyncPreviewDto
{
    public string ExternalObjectId { get; set; } = string.Empty;
    public string ExternalObjectType { get; set; } = string.Empty;
    public List<SyncPreviewField> Fields { get; set; } = new();
}

public class SyncPreviewField
{
    public string ExternalFieldName { get; set; } = string.Empty;
    public string? ExternalValue { get; set; }
    public string InternalFieldCode { get; set; } = string.Empty;
    public string InternalFieldLabelAr { get; set; } = string.Empty;
    public string? CurrentInternalValue { get; set; }
    public bool WouldUpdate { get; set; }
    public string ConflictStrategy { get; set; } = string.Empty;
    public bool IsConflict { get; set; }
}

// ── Request models ────────────────────────────────────────────
public class ResolveConflictRequest { public int FieldId { get; set; } public string Resolution { get; set; } = string.Empty; }
public class SyncPreviewRequest { public string ExternalObjectId { get; set; } = string.Empty; public string ExternalObjectType { get; set; } = string.Empty; }
