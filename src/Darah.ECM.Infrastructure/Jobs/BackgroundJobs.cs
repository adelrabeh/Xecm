using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.Jobs;

/// <summary>Runs every 15 minutes — marks overdue workflow tasks and triggers escalation.</summary>
public sealed class SlaCheckerJob
{
    private readonly IWorkflowRepository _workflowRepo;
    private readonly IAuditService       _audit;
    private readonly IEmailService       _email;
    private readonly ILogger<SlaCheckerJob> _logger;

    public SlaCheckerJob(IWorkflowRepository workflowRepo, IAuditService audit,
        IEmailService email, ILogger<SlaCheckerJob> logger)
    {
        _workflowRepo = workflowRepo;
        _audit        = audit;
        _email        = email;
        _logger       = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("SLA check started at {Time}", DateTime.UtcNow);
        var overdueTasks = await _workflowRepo.GetOverdueTasksAsync();
        int count = 0;

        foreach (var task in overdueTasks)
        {
            task.MarkOverdue();
            if (!task.SLABreachNotifiedAt.HasValue)
            {
                task.MarkSLABreachNotified();
                _logger.LogWarning("SLA breached: TaskId={TaskId}, DueAt={DueAt}", task.TaskId, task.DueAt);
                await _audit.LogAsync("SLABreached", "WorkflowTask", task.TaskId.ToString(),
                    severity: "Warning",
                    additionalInfo: $"DueAt: {task.DueAt}, AssignedTo: {task.AssignedToUserId}");
            }
            count++;
        }

        _logger.LogInformation("SLA check complete: {Count} tasks marked overdue", count);
    }
}

/// <summary>Runs nightly at 02:00 UTC — finds documents past retention expiry.</summary>
public sealed class RetentionPolicyJob
{
    private readonly IDocumentRepository _documentRepo;
    private readonly IAuditService       _audit;
    private readonly ILogger<RetentionPolicyJob> _logger;

    public RetentionPolicyJob(IDocumentRepository documentRepo, IAuditService audit,
        ILogger<RetentionPolicyJob> logger)
    {
        _documentRepo = documentRepo;
        _audit        = audit;
        _logger       = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Retention policy job started at {Time}", DateTime.UtcNow);
        var expiredDocs = await _documentRepo.GetExpiringRetentionAsync(daysAhead: 0);
        int count = 0;

        foreach (var doc in expiredDocs)
        {
            _logger.LogWarning("Retention expired: DocumentNumber={Number}", doc.DocumentNumber);
            await _audit.LogAsync("RetentionExpired", "Document", doc.DocumentId.ToString(),
                severity: "Warning",
                additionalInfo: $"RetentionExpiry: {doc.RetentionExpiresAt}");
            count++;
        }

        _logger.LogInformation("Retention job complete: {Count} documents flagged", count);
    }
}
