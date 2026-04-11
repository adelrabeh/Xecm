// ================================================================
// FILE: src/Domain/Entities/Workspace.cs
// DARAH ECM — xECM Extension: Business Workspace Layer
// ================================================================
namespace Darah.ECM.Domain.Entities;

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

    // ── External binding (xECM core) ──────────────────────────
    public string? ExternalSystemId { get; private set; }   // 'SAP_PROD', 'SF_CRM', etc.
    public string? ExternalObjectId { get; private set; }   // e.g. SAP WBS element ID
    public string? ExternalObjectType { get; private set; } // 'Project', 'Account', etc.
    public string? ExternalObjectUrl { get; private set; }  // deep-link back to source
    public DateTime? LastSyncedAt { get; private set; }
    public string? SyncStatus { get; private set; }         // Pending|Synced|Failed|Conflict
    public string? SyncError { get; private set; }

    // ── Status & lifecycle ─────────────────────────────────────
    public int StatusValueId { get; private set; }
    public int ClassificationLevelId { get; private set; } = 1;
    public bool IsLegalHold { get; private set; } = false;
    public int? RetentionPolicyId { get; private set; }
    public DateOnly? RetentionExpiresAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public int? ArchivedBy { get; private set; }

    // ── Navigation ─────────────────────────────────────────────
    public virtual WorkspaceType WorkspaceType { get; private set; } = null!;
    public virtual User Owner { get; private set; } = null!;
    public virtual ICollection<WorkspaceDocument> WorkspaceDocuments { get; private set; } = new HashSet<WorkspaceDocument>();
    public virtual ICollection<WorkspaceMetadataValue> MetadataValues { get; private set; } = new HashSet<WorkspaceMetadataValue>();
    public virtual ICollection<WorkspaceSecurityPolicy> SecurityPolicies { get; private set; } = new HashSet<WorkspaceSecurityPolicy>();

    private Workspace() { }

    public static Workspace Create(
        string titleAr,
        int workspaceTypeId,
        int ownerId,
        int statusValueId,
        string workspaceNumber,
        int createdBy,
        string? titleEn = null,
        int? departmentId = null,
        string? description = null,
        int classificationLevelId = 1,
        int? retentionPolicyId = null)
    {
        var ws = new Workspace
        {
            WorkspaceId = Guid.NewGuid(),
            WorkspaceNumber = workspaceNumber,
            TitleAr = titleAr,
            TitleEn = titleEn,
            WorkspaceTypeId = workspaceTypeId,
            OwnerId = ownerId,
            DepartmentId = departmentId,
            Description = description,
            StatusValueId = statusValueId,
            ClassificationLevelId = classificationLevelId,
            RetentionPolicyId = retentionPolicyId,
        };
        ws.SetCreated(createdBy);
        return ws;
    }

    // ── External binding ───────────────────────────────────────
    public void BindToExternal(string systemId, string objectId, string objectType, string? objectUrl, int updatedBy)
    {
        ExternalSystemId = systemId;
        ExternalObjectId = objectId;
        ExternalObjectType = objectType;
        ExternalObjectUrl = objectUrl;
        SyncStatus = "Pending";
        SetUpdated(updatedBy);
    }

    public void RecordSyncSuccess(DateTime syncedAt, int updatedBy)
    {
        LastSyncedAt = syncedAt;
        SyncStatus = "Synced";
        SyncError = null;
        SetUpdated(updatedBy);
    }

    public void RecordSyncFailure(string errorMessage, int updatedBy)
    {
        SyncStatus = "Failed";
        SyncError = errorMessage;
        SetUpdated(updatedBy);
    }

    public void RecordSyncConflict(int updatedBy)
    {
        SyncStatus = "Conflict";
        SetUpdated(updatedBy);
    }

    // ── Lifecycle ──────────────────────────────────────────────
    public void UpdateStatus(int statusValueId, int updatedBy)
    {
        StatusValueId = statusValueId;
        SetUpdated(updatedBy);
    }

    public void Archive(int archivedBy)
    {
        ArchivedAt = DateTime.UtcNow;
        ArchivedBy = archivedBy;
        SetUpdated(archivedBy);
    }

    public void ApplyLegalHold() => IsLegalHold = true;
    public void ReleaseLegalHold() => IsLegalHold = false;

    public void UpdateTitle(string titleAr, string? titleEn, int updatedBy)
    {
        TitleAr = titleAr; TitleEn = titleEn; SetUpdated(updatedBy);
    }

    public void SetRetentionExpiry(DateOnly expiry, int updatedBy)
    {
        RetentionExpiresAt = expiry; SetUpdated(updatedBy);
    }

    // ── Business rule: is external-bound? ─────────────────────
    public bool IsBoundToExternal =>
        !string.IsNullOrEmpty(ExternalSystemId) && !string.IsNullOrEmpty(ExternalObjectId);
}

// ================================================================
// FILE: src/Domain/Entities/WorkspaceType.cs
// ================================================================
namespace Darah.ECM.Domain.Entities;

public class WorkspaceType : BaseEntity
{
    public int TypeId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool AutoCreateOnExternal { get; private set; } = false;
    public bool InheritRetention { get; private set; } = true;
    public bool InheritSecurity { get; private set; } = true;
    public bool InheritWorkflow { get; private set; } = false;
    public string? DefaultExternalSystem { get; private set; }
    public string? ExternalObjectType { get; private set; }
    public int? DefaultRetentionPolicyId { get; private set; }
    public int ClassificationLevelId { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;
    public bool IsSystem { get; private set; } = false;
}

// ================================================================
// FILE: src/Domain/Entities/ExternalSystem.cs
// ================================================================
namespace Darah.ECM.Domain.Entities;

public class ExternalSystem : BaseEntity
{
    public int SystemId { get; private set; }
    public string SystemCode { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string SystemType { get; private set; } = string.Empty;  // SAP, Salesforce, Oracle, Custom
    public string? BaseUrl { get; private set; }
    public string AuthType { get; private set; } = "OAuth2";
    public string? CredentialRef { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastConnectedAt { get; private set; }
    public string? ConnectionStatus { get; private set; }
    public string? ConnectionError { get; private set; }
    public int SyncIntervalMinutes { get; private set; } = 60;
    public int RetryCount { get; private set; } = 3;
    public int TimeoutSeconds { get; private set; } = 30;

    public void RecordConnection(bool success, string? error = null)
    {
        LastConnectedAt = DateTime.UtcNow;
        ConnectionStatus = success ? "Connected" : "Error";
        ConnectionError = error;
    }
}

// ================================================================
// FILE: src/Domain/Entities/MetadataSyncMapping.cs
// ================================================================
namespace Darah.ECM.Domain.Entities;

public class MetadataSyncMapping : BaseEntity
{
    public int MappingId { get; private set; }
    public int ExternalSystemId { get; private set; }
    public int? WorkspaceTypeId { get; private set; }
    public string ExternalObjectType { get; private set; } = string.Empty;
    public string ExternalFieldName { get; private set; } = string.Empty;
    public string ExternalFieldType { get; private set; } = string.Empty;
    public int InternalFieldId { get; private set; }
    public string SyncDirection { get; private set; } = "InboundOnly";
    public string? TransformExpression { get; private set; }
    public string? DefaultValue { get; private set; }
    public bool IsRequired { get; private set; } = false;
    public string ConflictStrategy { get; private set; } = "ExternalWins";
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; } = 0;

    public virtual ExternalSystem ExternalSystem { get; private set; } = null!;
    public virtual MetadataField InternalField { get; private set; } = null!;
}

// ================================================================
// FILE: src/Application/Workspaces/Commands/CreateWorkspaceCommand.cs
// ================================================================
namespace Darah.ECM.Application.Workspaces.Commands;

public record CreateWorkspaceCommand : IRequest<ApiResponse<WorkspaceDto>>
{
    public string TitleAr { get; init; } = string.Empty;
    public string? TitleEn { get; init; }
    public int WorkspaceTypeId { get; init; }
    public int OwnerId { get; init; }
    public int? DepartmentId { get; init; }
    public string? Description { get; init; }
    public int ClassificationLevelId { get; init; } = 1;
    public int? RetentionPolicyId { get; init; }
    // Optional: bind to external system at creation
    public string? ExternalSystemId { get; init; }
    public string? ExternalObjectId { get; init; }
    public string? ExternalObjectType { get; init; }
    public string? ExternalObjectUrl { get; init; }
    public Dictionary<int, string> MetadataValues { get; init; } = new();
}

public class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(500).WithMessage("عنوان مساحة العمل مطلوب");
        RuleFor(x => x.WorkspaceTypeId).GreaterThan(0).WithMessage("يجب تحديد نوع مساحة العمل");
        RuleFor(x => x.OwnerId).GreaterThan(0).WithMessage("يجب تحديد مالك مساحة العمل");
        RuleFor(x => x.ClassificationLevelId).InclusiveBetween(1, 4);
        When(x => !string.IsNullOrEmpty(x.ExternalSystemId), () =>
        {
            RuleFor(x => x.ExternalObjectId).NotEmpty().WithMessage("معرف الكيان الخارجي مطلوب عند تحديد النظام الخارجي");
            RuleFor(x => x.ExternalObjectType).NotEmpty().WithMessage("نوع الكيان الخارجي مطلوب");
        });
    }
}

public class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, ApiResponse<WorkspaceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IWorkspaceNumberGenerator _numberGenerator;
    private readonly IMetadataSyncEngine _syncEngine;

    public CreateWorkspaceCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IAuditService audit,
        IWorkspaceNumberGenerator numberGenerator,
        IMetadataSyncEngine syncEngine)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _numberGenerator = numberGenerator;
        _syncEngine = syncEngine;
    }

    public async Task<ApiResponse<WorkspaceDto>> Handle(CreateWorkspaceCommand request, CancellationToken ct)
    {
        // Validate workspace type
        var wsType = await _context.WorkspaceTypes
            .FirstOrDefaultAsync(t => t.TypeId == request.WorkspaceTypeId && t.IsActive, ct);
        if (wsType == null)
            return ApiResponse<WorkspaceDto>.Fail("نوع مساحة العمل غير موجود أو غير نشط");

        // Validate external system if provided
        if (!string.IsNullOrEmpty(request.ExternalSystemId))
        {
            var exists = await _context.ExternalSystems
                .AnyAsync(es => es.SystemCode == request.ExternalSystemId && es.IsActive, ct);
            if (!exists)
                return ApiResponse<WorkspaceDto>.Fail($"النظام الخارجي '{request.ExternalSystemId}' غير موجود أو غير نشط");

            // Prevent duplicate binding
            var alreadyBound = await _context.Workspaces.AnyAsync(w =>
                w.ExternalSystemId == request.ExternalSystemId &&
                w.ExternalObjectId == request.ExternalObjectId && !w.IsDeleted, ct);
            if (alreadyBound)
                return ApiResponse<WorkspaceDto>.Fail("يوجد مساحة عمل مرتبطة بهذا الكيان الخارجي بالفعل");
        }

        // Get active status
        var activeStatus = await _context.LookupValues
            .Where(lv => lv.Category.Code == "WS_STATUS" && lv.Code == "WS_ACTIVE")
            .Select(lv => lv.ValueId).FirstOrDefaultAsync(ct);

        // Inherit defaults from workspace type
        var retentionPolicyId = request.RetentionPolicyId ?? wsType.DefaultRetentionPolicyId;
        var classificationLevelId = request.ClassificationLevelId;

        // Generate workspace number
        var wsNumber = await _numberGenerator.GenerateAsync(request.WorkspaceTypeId, ct);

        // Create workspace
        var workspace = Workspace.Create(
            request.TitleAr, request.WorkspaceTypeId, request.OwnerId, activeStatus,
            wsNumber, _currentUser.UserId, request.TitleEn, request.DepartmentId,
            request.Description, classificationLevelId, retentionPolicyId);

        // Bind to external if provided
        if (!string.IsNullOrEmpty(request.ExternalSystemId))
            workspace.BindToExternal(request.ExternalSystemId, request.ExternalObjectId!,
                request.ExternalObjectType!, request.ExternalObjectUrl, _currentUser.UserId);

        _context.Workspaces.Add(workspace);

        // Inherit security policies from workspace type defaults
        if (wsType.InheritSecurity)
            await InheritSecurityPoliciesAsync(workspace, wsType, ct);

        // Save metadata values
        foreach (var (fieldId, value) in request.MetadataValues)
        {
            _context.WorkspaceMetadataValues.Add(new WorkspaceMetadataValue
            {
                WorkspaceId = workspace.WorkspaceId,
                FieldId = fieldId,
                TextValue = value,
                SourceType = "Manual"
            });
        }

        await _context.SaveChangesAsync(ct);

        // If external binding exists, trigger initial metadata sync
        if (workspace.IsBoundToExternal)
            await _syncEngine.TriggerSyncAsync(workspace.WorkspaceId, SyncDirection.Inbound, ct);

        await _audit.LogAsync("WorkspaceCreated", "Workspace", workspace.WorkspaceId.ToString(),
            newValues: new { workspace.WorkspaceNumber, workspace.TitleAr, workspace.ExternalSystemId, workspace.ExternalObjectId });

        var dto = await BuildDtoAsync(workspace, ct);
        return ApiResponse<WorkspaceDto>.Ok(dto, "تم إنشاء مساحة العمل بنجاح");
    }

    private async Task InheritSecurityPoliciesAsync(Workspace workspace, WorkspaceType wsType, CancellationToken ct)
    {
        // If department-based workspace → grant department access
        if (workspace.DepartmentId.HasValue)
        {
            _context.WorkspaceSecurityPolicies.Add(new WorkspaceSecurityPolicy
            {
                WorkspaceId = workspace.WorkspaceId,
                PrincipalType = "Department",
                PrincipalId = workspace.DepartmentId.Value,
                CanRead = true,
                CanWrite = true,
                CanDownload = true,
                InheritToDocuments = wsType.InheritSecurity,
                GrantedBy = _currentUser.UserId
            });
        }
    }

    private async Task<WorkspaceDto> BuildDtoAsync(Workspace ws, CancellationToken ct)
    {
        return new WorkspaceDto
        {
            WorkspaceId = ws.WorkspaceId,
            WorkspaceNumber = ws.WorkspaceNumber,
            TitleAr = ws.TitleAr,
            TitleEn = ws.TitleEn,
            ExternalSystemId = ws.ExternalSystemId,
            ExternalObjectId = ws.ExternalObjectId,
            ExternalObjectType = ws.ExternalObjectType,
            SyncStatus = ws.SyncStatus,
            LastSyncedAt = ws.LastSyncedAt,
            CreatedAt = ws.CreatedAt,
            IsBoundToExternal = ws.IsBoundToExternal
        };
    }
}

// ================================================================
// FILE: src/Application/Workspaces/Commands/BindExternalObjectCommand.cs
// ================================================================
namespace Darah.ECM.Application.Workspaces.Commands;

public record BindExternalObjectCommand : IRequest<ApiResponse<bool>>
{
    public Guid WorkspaceId { get; init; }
    public string ExternalSystemId { get; init; } = string.Empty;
    public string ExternalObjectId { get; init; } = string.Empty;
    public string ExternalObjectType { get; init; } = string.Empty;
    public string? ExternalObjectUrl { get; init; }
    public bool TriggerImmediateSync { get; init; } = true;
}

public class BindExternalObjectCommandHandler : IRequestHandler<BindExternalObjectCommand, ApiResponse<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IMetadataSyncEngine _syncEngine;

    public BindExternalObjectCommandHandler(IApplicationDbContext context, ICurrentUser currentUser, IAuditService audit, IMetadataSyncEngine syncEngine)
    {
        _context = context; _currentUser = currentUser; _audit = audit; _syncEngine = syncEngine;
    }

    public async Task<ApiResponse<bool>> Handle(BindExternalObjectCommand request, CancellationToken ct)
    {
        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.WorkspaceId == request.WorkspaceId && !w.IsDeleted, ct);

        if (workspace == null) return ApiResponse<bool>.Fail("مساحة العمل غير موجودة");

        if (workspace.IsBoundToExternal)
            return ApiResponse<bool>.Fail($"مساحة العمل مرتبطة بالفعل بـ {workspace.ExternalSystemId}/{workspace.ExternalObjectId}");

        var systemExists = await _context.ExternalSystems
            .AnyAsync(es => es.SystemCode == request.ExternalSystemId && es.IsActive, ct);
        if (!systemExists)
            return ApiResponse<bool>.Fail($"النظام الخارجي '{request.ExternalSystemId}' غير موجود");

        workspace.BindToExternal(request.ExternalSystemId, request.ExternalObjectId,
            request.ExternalObjectType, request.ExternalObjectUrl, _currentUser.UserId);

        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync("WorkspaceLinkedToExternal", "Workspace", workspace.WorkspaceId.ToString(),
            newValues: new { request.ExternalSystemId, request.ExternalObjectId });

        if (request.TriggerImmediateSync)
            await _syncEngine.TriggerSyncAsync(request.WorkspaceId, SyncDirection.Inbound, ct);

        return ApiResponse<bool>.Ok(true, "تم ربط مساحة العمل بالكيان الخارجي بنجاح. جارٍ مزامنة البيانات الوصفية...");
    }
}

// ================================================================
// FILE: src/Application/Workspaces/Commands/AddDocumentToWorkspaceCommand.cs
// ================================================================
namespace Darah.ECM.Application.Workspaces.Commands;

public record AddDocumentToWorkspaceCommand : IRequest<ApiResponse<bool>>
{
    public Guid WorkspaceId { get; init; }
    public Guid DocumentId { get; init; }
    public string BindingType { get; init; } = "Primary";
}

public class AddDocumentToWorkspaceCommandHandler : IRequestHandler<AddDocumentToWorkspaceCommand, ApiResponse<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IWorkspaceSecurityService _securityService;

    public AddDocumentToWorkspaceCommandHandler(IApplicationDbContext context, ICurrentUser currentUser, IAuditService audit, IWorkspaceSecurityService securityService)
    {
        _context = context; _currentUser = currentUser; _audit = audit; _securityService = securityService;
    }

    public async Task<ApiResponse<bool>> Handle(AddDocumentToWorkspaceCommand request, CancellationToken ct)
    {
        var workspace = await _context.Workspaces
            .Include(w => w.WorkspaceType)
            .FirstOrDefaultAsync(w => w.WorkspaceId == request.WorkspaceId && !w.IsDeleted, ct);
        if (workspace == null) return ApiResponse<bool>.Fail("مساحة العمل غير موجودة");

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == request.DocumentId && !d.IsDeleted, ct);
        if (document == null) return ApiResponse<bool>.Fail("الوثيقة غير موجودة");

        // Permission check
        var canWrite = await _securityService.CanWriteWorkspaceAsync(request.WorkspaceId, _currentUser.UserId, ct);
        if (!canWrite) return ApiResponse<bool>.Fail("ليس لديك صلاحية إضافة وثائق لهذه مساحة العمل");

        // Prevent duplicates
        var alreadyLinked = await _context.WorkspaceDocuments
            .AnyAsync(wd => wd.WorkspaceId == request.WorkspaceId && wd.DocumentId == request.DocumentId && wd.IsActive, ct);
        if (alreadyLinked) return ApiResponse<bool>.Fail("الوثيقة مرتبطة بمساحة العمل هذه بالفعل");

        _context.WorkspaceDocuments.Add(new WorkspaceDocument
        {
            WorkspaceId = request.WorkspaceId,
            DocumentId = request.DocumentId,
            BindingType = request.BindingType,
            AddedBy = _currentUser.UserId,
            AddedAt = DateTime.UtcNow,
            IsActive = true
        });

        // Inherit workspace security to document if type requires it
        if (workspace.WorkspaceType.InheritSecurity)
            await _securityService.PropagateSecurityToDocumentAsync(workspace, request.DocumentId, ct);

        // Inherit workspace retention if configured
        if (workspace.WorkspaceType.InheritRetention && workspace.RetentionPolicyId.HasValue)
        {
            var policy = await _context.RetentionPolicies.FindAsync(new object[] { workspace.RetentionPolicyId.Value }, ct);
            if (policy != null)
            {
                var expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(policy.RetentionYears));
                document.SetRetentionExpiry(expiryDate, _currentUser.UserId);
                document.UpdateStatus(document.StatusValueId, _currentUser.UserId); // trigger update
            }
        }

        // Update document's primary workspace if not already set
        if (!document.PrimaryWorkspaceId.HasValue)
            document.SetPrimaryWorkspace(request.WorkspaceId, _currentUser.UserId);

        await _context.SaveChangesAsync(ct);
        await _audit.LogAsync("DocumentAddedToWorkspace", "WorkspaceDocument", request.WorkspaceId.ToString(),
            newValues: new { request.DocumentId, request.BindingType });

        return ApiResponse<bool>.Ok(true, "تم إضافة الوثيقة لمساحة العمل بنجاح");
    }
}

// ================================================================
// FILE: src/Application/Workspaces/DTOs/WorkspaceDto.cs
// ================================================================
namespace Darah.ECM.Application.Workspaces.DTOs;

public class WorkspaceDto
{
    public Guid WorkspaceId { get; set; }
    public string WorkspaceNumber { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? TypeNameAr { get; set; }
    public string? TypeNameEn { get; set; }
    public string? TypeCode { get; set; }
    public string? StatusAr { get; set; }
    public string? StatusEn { get; set; }
    public string? ClassificationAr { get; set; }
    public string? OwnerNameAr { get; set; }
    public string? DepartmentAr { get; set; }
    // External binding
    public bool IsBoundToExternal { get; set; }
    public string? ExternalSystemId { get; set; }
    public string? ExternalObjectId { get; set; }
    public string? ExternalObjectType { get; set; }
    public string? ExternalObjectUrl { get; set; }
    public string? SyncStatus { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    // Counts
    public int DocumentCount { get; set; }
    // Flags
    public bool IsLegalHold { get; set; }
    public DateOnly? RetentionExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<MetadataValueDto> MetadataValues { get; set; } = new();
}

public class WorkspaceListItemDto
{
    public Guid WorkspaceId { get; set; }
    public string WorkspaceNumber { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? TypeCode { get; set; }
    public string? TypeNameAr { get; set; }
    public string? StatusAr { get; set; }
    public string? ExternalSystemId { get; set; }
    public string? ExternalObjectId { get; set; }
    public string? SyncStatus { get; set; }
    public int DocumentCount { get; set; }
    public bool IsLegalHold { get; set; }
    public DateTime CreatedAt { get; set; }
}
