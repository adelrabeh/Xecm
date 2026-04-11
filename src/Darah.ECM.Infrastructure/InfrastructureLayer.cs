// ============================================================
// FILE: src/Infrastructure/Persistence/EcmDbContext.cs
// ============================================================
namespace Darah.ECM.Infrastructure.Persistence;

public class EcmDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUser? _currentUser;

    public EcmDbContext(DbContextOptions<EcmDbContext> options, ICurrentUser? currentUser = null)
        : base(options) { _currentUser = currentUser; }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();
    public DbSet<DocumentLibrary> DocumentLibraries => Set<DocumentLibrary>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<MetadataField> MetadataFields => Set<MetadataField>();
    public DbSet<DocumentMetadataValue> DocumentMetadataValues => Set<DocumentMetadataValue>();
    public DbSet<DocumentTag> DocumentTags => Set<DocumentTag>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<LookupCategory> LookupCategories => Set<LookupCategory>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<WorkflowAction> WorkflowActions => Set<WorkflowAction>();
    public DbSet<WorkflowDelegation> WorkflowDelegations => Set<WorkflowDelegation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();
    public DbSet<DocumentLegalHold> DocumentLegalHolds => Set<DocumentLegalHold>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<ClassificationLevel> ClassificationLevels => Set<ClassificationLevel>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EcmDbContext).Assembly);

        // Global query filter for soft delete on major entities
        modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<DocumentLibrary>().HasQueryFilter(l => !l.IsDeleted);
        modelBuilder.Entity<Folder>().HasQueryFilter(f => !f.IsDeleted);
        modelBuilder.Entity<DocumentType>().HasQueryFilter(dt => !dt.IsDeleted);

        // Configure GUID default for Documents
        modelBuilder.Entity<Document>().Property(d => d.DocumentId).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Configure AuditLog as append-only (no update/delete in EF tracking)
        modelBuilder.Entity<AuditLog>().ToTable("AuditLogs").HasKey(a => a.AuditId);
        modelBuilder.Entity<AuditLog>().Property(a => a.AuditId).ValueGeneratedOnAdd();

        // Full-text indexes configuration notes (apply via migrations/SQL)
        // SQL Server FTS is configured via SQL scripts, not EF Core model
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-set audit timestamps on modified entities
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (_currentUser?.IsAuthenticated == true && entry.Entity.CreatedBy == 0)
                        entry.Entity.SetCreated(_currentUser.UserId);
                    break;
                case EntityState.Modified:
                    if (_currentUser?.IsAuthenticated == true)
                        entry.Entity.SetUpdated(_currentUser.UserId);
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}

// ============================================================
// FILE: src/Infrastructure/Services/LocalFileStorageService.cs
// ============================================================
namespace Darah.ECM.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _basePath = configuration["Storage:LocalPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
        _logger = logger;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> StoreAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName);
        var storageKey = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_basePath, storageKey.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await fileStream.CopyToAsync(outputStream, 81920, ct);

        _logger.LogInformation("File stored: {StorageKey}", storageKey);
        return storageKey;
    }

    public async Task<Stream> RetrieveAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(storageKey);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"File not found: {storageKey}");
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(storageKey);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult(File.Exists(GetFullPath(storageKey)));

    public string GetProvider() => "LocalFileSystem";

    private string GetFullPath(string storageKey)
        => Path.GetFullPath(Path.Combine(_basePath, storageKey.Replace('/', Path.DirectorySeparatorChar)));
}

// ============================================================
// FILE: src/Infrastructure/Services/WorkflowEngineService.cs
// ============================================================
namespace Darah.ECM.Infrastructure.Services;

public class WorkflowEngineService : IWorkflowEngine
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IAuditService _audit;
    private readonly ILogger<WorkflowEngineService> _logger;

    public WorkflowEngineService(IApplicationDbContext context, IEmailService emailService, IAuditService audit, ILogger<WorkflowEngineService> logger)
    {
        _context = context; _emailService = emailService; _audit = audit; _logger = logger;
    }

    public async Task<int> StartAsync(Guid documentId, int definitionId, int initiatedBy, CancellationToken ct = default)
    {
        var definition = await _context.WorkflowDefinitions
            .Include(wd => wd.Steps.OrderBy(s => s.StepOrder))
            .FirstOrDefaultAsync(wd => wd.DefinitionId == definitionId && wd.IsActive, ct)
            ?? throw new InvalidOperationException($"Workflow definition {definitionId} not found or inactive.");

        var firstStep = definition.Steps.FirstOrDefault(s => s.IsFirstStep)
            ?? definition.Steps.OrderBy(s => s.StepOrder).First();

        var instance = WorkflowInstance.Start(definitionId, documentId, initiatedBy);
        _context.WorkflowInstances.Add(instance);
        await _context.SaveChangesAsync(ct);

        instance.MoveToStep(firstStep.StepId);
        await AssignTaskAsync(instance, firstStep, ct);
        await _context.SaveChangesAsync(ct);

        return instance.InstanceId;
    }

    public async Task<bool> ProcessActionAsync(int taskId, WorkflowActionType action, int actionBy, string? comment = null, int? delegateToUserId = null, CancellationToken ct = default)
    {
        var task = await _context.WorkflowTasks
            .Include(t => t.Instance).ThenInclude(i => i.Definition).ThenInclude(d => d.Steps)
            .Include(t => t.Step)
            .FirstOrDefaultAsync(t => t.TaskId == taskId && t.Status == "Pending", ct);

        if (task == null) return false;

        // Verify task belongs to the acting user
        if (task.AssignedToUserId != actionBy)
        {
            // Check role assignment
            var hasRole = task.AssignedToRoleId.HasValue && await _context.UserRoles
                .AnyAsync(ur => ur.UserId == actionBy && ur.RoleId == task.AssignedToRoleId.Value && ur.IsActive, ct);
            if (!hasRole) return false;
        }

        // Record the action
        var wfAction = new WorkflowAction { TaskId = taskId, ActionType = action.ToString(), Comment = comment, ActionAt = DateTime.UtcNow, ActionBy = actionBy, DelegatedToId = delegateToUserId };
        _context.WorkflowActions.Add(wfAction);
        task.Complete(actionBy);
        await _context.SaveChangesAsync(ct);

        switch (action)
        {
            case WorkflowActionType.Approve:
                await HandleApprovalAsync(task, ct);
                break;
            case WorkflowActionType.Reject:
                task.Instance.Reject();
                break;
            case WorkflowActionType.Return:
                await HandleReturnAsync(task, ct);
                break;
            case WorkflowActionType.Delegate:
                if (!delegateToUserId.HasValue) return false;
                var delegatedTask = WorkflowTask.Create(task.InstanceId, task.StepId, delegateToUserId.Value, null, task.Step.SLAHours);
                _context.WorkflowTasks.Add(delegatedTask);
                break;
            case WorkflowActionType.Escalate:
                await HandleEscalationAsync(task, ct);
                break;
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    private async Task HandleApprovalAsync(WorkflowTask task, CancellationToken ct)
    {
        var steps = task.Instance.Definition.Steps.OrderBy(s => s.StepOrder).ToList();
        var currentStepIndex = steps.FindIndex(s => s.StepId == task.StepId);

        if (task.Step.IsFinalStep || currentStepIndex >= steps.Count - 1)
        {
            task.Instance.Complete();
            await NotifyWorkflowCompleted(task.Instance, ct);
        }
        else
        {
            var nextStep = steps[currentStepIndex + 1];
            task.Instance.MoveToStep(nextStep.StepId);
            await AssignTaskAsync(task.Instance, nextStep, ct);
        }
    }

    private async Task HandleReturnAsync(WorkflowTask task, CancellationToken ct)
    {
        var steps = task.Instance.Definition.Steps.OrderBy(s => s.StepOrder).ToList();
        var currentStepIndex = steps.FindIndex(s => s.StepId == task.StepId);

        if (currentStepIndex <= 0)
        {
            // Return to originator
            task.Instance.Reject();
        }
        else
        {
            var prevStep = steps[currentStepIndex - 1];
            task.Instance.MoveToStep(prevStep.StepId);
            await AssignTaskAsync(task.Instance, prevStep, ct);
        }
    }

    private async Task HandleEscalationAsync(WorkflowTask task, CancellationToken ct)
    {
        if (!task.Step.EscalationUserId.HasValue) return;
        var escalatedTask = WorkflowTask.Create(task.InstanceId, task.StepId, task.Step.EscalationUserId.Value, null, null);
        escalatedTask.MarkEscalated();
        _context.WorkflowTasks.Add(escalatedTask);
        await SendNotificationAsync(task.Step.EscalationUserId.Value, "تصعيد مهمة", $"تم تصعيد مهمة إليك في مسار العمل: {task.Instance.Definition.NameAr}", ct);
    }

    private async Task AssignTaskAsync(WorkflowInstance instance, WorkflowStep step, CancellationToken ct)
    {
        var (userId, roleId) = await ResolveAssigneeAsync(step, instance.DocumentId, ct);
        var task = WorkflowTask.Create(instance.InstanceId, step.StepId, userId, roleId, step.SLAHours);
        _context.WorkflowTasks.Add(task);

        if (userId.HasValue && step.NotifyOnAssign)
            await SendNotificationAsync(userId.Value, "مهمة جديدة بانتظارك", $"تم تعيين مهمة جديدة لك في مسار: {instance.Definition.NameAr}. الرجاء المراجعة.", ct);
    }

    private async Task<(int? userId, int? roleId)> ResolveAssigneeAsync(WorkflowStep step, Guid documentId, CancellationToken ct)
    {
        return step.AssigneeType switch
        {
            "SpecificUser" => (step.AssigneeId, null),
            "Role" => (null, step.AssigneeRoleId),
            "Department" => await ResolveDepartmentManagerAsync(step.AssigneeDeptId!.Value, ct),
            "Dynamic" => await ResolveDynamicAssigneeAsync(step, documentId, ct),
            _ => (step.AssigneeId, step.AssigneeRoleId)
        };
    }

    private async Task<(int? userId, int? roleId)> ResolveDepartmentManagerAsync(int deptId, CancellationToken ct)
    {
        var managerId = await _context.Departments.Where(d => d.DepartmentId == deptId).Select(d => d.ManagerId).FirstOrDefaultAsync(ct);
        return (managerId, null);
    }

    private async Task<(int? userId, int? roleId)> ResolveDynamicAssigneeAsync(WorkflowStep step, Guid documentId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(step.DynamicFieldCode)) return (step.AssigneeId, step.AssigneeRoleId);

        var field = await _context.MetadataFields.FirstOrDefaultAsync(f => f.FieldCode == step.DynamicFieldCode, ct);
        if (field == null) return (step.AssigneeId, step.AssigneeRoleId);

        var metaValue = await _context.DocumentMetadataValues
            .Where(mv => mv.DocumentId == documentId && mv.FieldId == field.FieldId)
            .Select(mv => mv.TextValue).FirstOrDefaultAsync(ct);

        if (int.TryParse(metaValue, out var resolvedUserId)) return (resolvedUserId, null);
        return (step.AssigneeId, step.AssigneeRoleId);
    }

    public async Task CheckSLABreachesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var overdueTasks = await _context.WorkflowTasks
            .Include(t => t.Step).Include(t => t.Instance).ThenInclude(i => i.Definition)
            .Where(t => t.Status == "Pending" && t.DueAt.HasValue && t.DueAt.Value < now && !t.IsOverdue)
            .ToListAsync(ct);

        foreach (var task in overdueTasks)
        {
            task.MarkOverdue();

            if (!task.SLABreachNotifiedAt.HasValue)
            {
                task.MarkSLABreachNotified();
                if (task.AssignedToUserId.HasValue)
                    await SendNotificationAsync(task.AssignedToUserId.Value, "تنبيه: تجاوز SLA", $"انتهت مهلة مهمتك في مسار: {task.Instance.Definition.NameAr}. يرجى الإجراء فوراً.", ct);
            }

            // Check escalation
            if (task.Step.EscalationHours.HasValue)
            {
                var escalationDue = task.AssignedAt.AddHours(task.Step.SLAHours!.Value + task.Step.EscalationHours.Value);
                if (now > escalationDue && !task.EscalatedAt.HasValue)
                    await HandleEscalationAsync(task, ct);
            }

            await _audit.LogAsync("SLABreached", "WorkflowTask", task.TaskId.ToString(), severity: "Warning");
        }

        if (overdueTasks.Any()) await _context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<WorkflowTask>> GetUserInboxAsync(int userId, CancellationToken ct = default)
    {
        var userRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId && ur.IsActive).Select(ur => ur.RoleId).ToListAsync(ct);

        return await _context.WorkflowTasks
            .Include(t => t.Instance).ThenInclude(i => i.Document)
            .Include(t => t.Instance).ThenInclude(i => i.Definition)
            .Include(t => t.Step)
            .Where(t => t.Status == "Pending" &&
                (t.AssignedToUserId == userId || userRoleIds.Contains(t.AssignedToRoleId ?? 0)))
            .OrderBy(t => t.IsOverdue ? 0 : 1).ThenBy(t => t.DueAt)
            .ToListAsync(ct);
    }

    private async Task NotifyWorkflowCompleted(WorkflowInstance instance, CancellationToken ct)
    {
        var document = await _context.Documents.FindAsync(new object[] { instance.DocumentId }, ct);
        if (document != null)
            await _audit.LogAsync("WorkflowCompleted", "WorkflowInstance", instance.InstanceId.ToString());
    }

    private async Task SendNotificationAsync(int userId, string title, string body, CancellationToken ct)
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            NotificationType = "WorkflowAlert",
            Priority = 3,
            CreatedAt = DateTime.UtcNow
        });
    }
}

// ============================================================
// FILE: src/Infrastructure/Services/AuditService.cs
// ============================================================
namespace Darah.ECM.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IApplicationDbContext context, ICurrentUser currentUser, ILogger<AuditService> logger)
    {
        _context = context; _currentUser = currentUser; _logger = logger;
    }

    public async Task LogAsync(string eventType, string? entityType = null, string? entityId = null, object? oldValues = null, object? newValues = null, string severity = "Info", bool isSuccessful = true, string? failureReason = null, string? additionalInfo = null)
    {
        try
        {
            var log = AuditLog.Create(
                eventType: eventType,
                entityType: entityType,
                entityId: entityId,
                userId: _currentUser.IsAuthenticated ? _currentUser.UserId : null,
                username: _currentUser.IsAuthenticated ? _currentUser.Username : null,
                ipAddress: _currentUser.IPAddress,
                oldValues: oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                newValues: newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
                additionalInfo: additionalInfo,
                severity: severity,
                isSuccessful: isSuccessful,
                failureReason: failureReason,
                sessionId: _currentUser.SessionId
            );

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for event {EventType}", eventType);
        }
    }
}

// ============================================================
// FILE: src/Infrastructure/Services/SmtpEmailService.cs
// ============================================================
namespace Darah.ECM.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config; _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string? toName = null, CancellationToken ct = default)
    {
        var host = _config["Email:SmtpHost"];
        var port = int.Parse(_config["Email:SmtpPort"] ?? "587");
        var username = _config["Email:SmtpUsername"];
        var password = _config["Email:SmtpPassword"];
        var fromEmail = _config["Email:FromAddress"] ?? "noreply@darah.gov.sa";
        var fromName = _config["Email:FromName"] ?? "DARAH ECM";

        using var client = new System.Net.Mail.SmtpClient(host, port)
        {
            Credentials = new System.Net.NetworkCredential(username, password),
            EnableSsl = true
        };

        var message = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new System.Net.Mail.MailAddress(toEmail, toName ?? toEmail));

        try
        {
            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email sent to {Email}, subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }
}

// ============================================================
// FILE: src/Infrastructure/Jobs/SlaCheckerJob.cs
// ============================================================
namespace Darah.ECM.Infrastructure.Jobs;

public class SlaCheckerJob
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ILogger<SlaCheckerJob> _logger;

    public SlaCheckerJob(IWorkflowEngine workflowEngine, ILogger<SlaCheckerJob> logger)
    {
        _workflowEngine = workflowEngine; _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task CheckSLABreachesAsync()
    {
        _logger.LogInformation("Running SLA breach check at {Time}", DateTime.UtcNow);
        await _workflowEngine.CheckSLABreachesAsync();
        _logger.LogInformation("SLA breach check completed.");
    }
}

public class RetentionPolicyJob
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _audit;
    private readonly ILogger<RetentionPolicyJob> _logger;

    public RetentionPolicyJob(IApplicationDbContext context, IAuditService audit, ILogger<RetentionPolicyJob> logger)
    {
        _context = context; _audit = audit; _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task ProcessRetentionAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiredDocs = await _context.Documents
            .Where(d => !d.IsDeleted && !d.IsLegalHold && d.RetentionExpiresAt.HasValue && d.RetentionExpiresAt.Value <= today)
            .Take(100)
            .ToListAsync();

        foreach (var doc in expiredDocs)
        {
            _logger.LogWarning("Document {DocNumber} has exceeded retention period. Flagging for disposal review.", doc.DocumentNumber);
            await _audit.LogAsync("RetentionExpired", "Document", doc.DocumentId.ToString(), severity: "Warning", additionalInfo: $"RetentionExpiry: {doc.RetentionExpiresAt}");
        }
    }
}
