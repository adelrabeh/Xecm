using Darah.ECM.Application.Common.Models;
using Darah.ECM.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Darah.ECM.Infrastructure.Workflow;

/// <summary>
/// BPMN 2.0 Workflow Engine.
/// Supports: Flowable REST API integration + built-in simple engine fallback.
/// Compatible with: Flowable, Camunda, Activiti process definitions.
/// </summary>
public interface IBpmnEngine
{
    Task<string> DeployProcessAsync(string bpmnXml, string processName,
        CancellationToken ct);
    Task<string> StartProcessAsync(string processDefinitionKey,
        Guid documentId, int startedBy, Dictionary<string, object> variables,
        CancellationToken ct);
    Task CompleteTaskAsync(string taskId, string action, string? comment,
        int userId, CancellationToken ct);
    Task<IEnumerable<BpmnTask>> GetUserTasksAsync(int userId, CancellationToken ct);
    Task<ProcessStatus> GetProcessStatusAsync(string instanceId, CancellationToken ct);
}

public record BpmnTask(
    string TaskId, string Name, string ProcessInstanceId,
    string AssigneeId, DateTime Created, DateTime? DueDate,
    string Priority, Dictionary<string, object> Variables);

public record ProcessStatus(
    string InstanceId, string ProcessDefinitionKey,
    string Status, DateTime StartedAt, DateTime? EndedAt,
    IEnumerable<string> ActiveTasks);

/// <summary>
/// Flowable REST API integration.
/// Requires: Flowable server running at configured endpoint.
/// </summary>
public sealed class FlowableEngine : IBpmnEngine
{
    private readonly HttpClient _http;
    private readonly ILogger<FlowableEngine> _log;
    private readonly string _baseUrl;

    public FlowableEngine(HttpClient http, IConfiguration config,
        ILogger<FlowableEngine> log)
    {
        _http = http;
        _log = log;
        _baseUrl = config["Flowable:BaseUrl"] ?? "http://localhost:8090/flowable-rest/service";
        var user = config["Flowable:Username"] ?? "rest-admin";
        var pass = config["Flowable:Password"] ?? "test";
        var creds = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{user}:{pass}"));
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
    }

    public async Task<string> DeployProcessAsync(string bpmnXml,
        string processName, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var content = new StringContent(bpmnXml);
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
        form.Add(content, "file", $"{processName}.bpmn20.xml");

        var res = await _http.PostAsync($"{_baseUrl}/repository/deployments", form, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var deploymentId = doc.RootElement.GetProperty("id").GetString()!;

        _log.LogInformation("BPMN process deployed: {Name} → {Id}",
            processName, deploymentId);
        return deploymentId;
    }

    public async Task<string> StartProcessAsync(string processDefinitionKey,
        Guid documentId, int startedBy, Dictionary<string, object> variables,
        CancellationToken ct)
    {
        variables["documentId"] = documentId.ToString();
        variables["startedBy"] = startedBy.ToString();
        variables["startedAt"] = DateTime.UtcNow.ToString("O");

        var body = JsonSerializer.Serialize(new
        {
            processDefinitionKey,
            variables = variables.Select(kv => new
            {
                name = kv.Key,
                value = kv.Value.ToString(),
                type = "string"
            })
        });

        var res = await _http.PostAsync(
            $"{_baseUrl}/runtime/process-instances",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var instanceId = doc.RootElement.GetProperty("id").GetString()!;

        _log.LogInformation(
            "BPMN process started: {Key} for doc {DocId} → instance {Id}",
            processDefinitionKey, documentId, instanceId);

        return instanceId;
    }

    public async Task CompleteTaskAsync(string taskId, string action,
        string? comment, int userId, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            action = "complete",
            variables = new[]
            {
                new { name = "action", value = action, type = "string" },
                new { name = "comment", value = comment ?? "", type = "string" },
                new { name = "completedBy", value = userId.ToString(), type = "string" },
                new { name = "completedAt", value = DateTime.UtcNow.ToString("O"), type = "string" }
            }
        });

        var res = await _http.PostAsync(
            $"{_baseUrl}/runtime/tasks/{taskId}",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();

        _log.LogInformation("BPMN task {TaskId} completed with action {Action}", taskId, action);
    }

    public async Task<IEnumerable<BpmnTask>> GetUserTasksAsync(int userId,
        CancellationToken ct)
    {
        var res = await _http.GetAsync(
            $"{_baseUrl}/runtime/tasks?assignee={userId}&size=50", ct);
        if (!res.IsSuccessStatusCode) return [];

        var json = await res.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var tasks = doc.RootElement.GetProperty("data").EnumerateArray();

        return tasks.Select(t => new BpmnTask(
            t.GetProperty("id").GetString()!,
            t.GetProperty("name").GetString()!,
            t.GetProperty("processInstanceId").GetString()!,
            userId.ToString(),
            t.GetProperty("createTime").GetDateTime(),
            t.TryGetProperty("dueDate", out var due) && due.ValueKind != JsonValueKind.Null
                ? due.GetDateTime() : null,
            t.TryGetProperty("priority", out var pri) ? pri.GetInt32().ToString() : "50",
            new Dictionary<string, object>())).ToList();
    }

    public async Task<ProcessStatus> GetProcessStatusAsync(string instanceId,
        CancellationToken ct)
    {
        var res = await _http.GetAsync(
            $"{_baseUrl}/runtime/process-instances/{instanceId}", ct);
        if (!res.IsSuccessStatusCode)
            return new ProcessStatus(instanceId, "unknown", "notFound",
                DateTime.UtcNow, null, []);

        var json = await res.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new ProcessStatus(
            instanceId,
            root.GetProperty("processDefinitionKey").GetString()!,
            root.GetProperty("ended").GetBoolean() ? "Completed" : "Running",
            DateTime.UtcNow, null, []);
    }
}

/// <summary>
/// Built-in BPMN fallback engine (no external server needed).
/// Used when Flowable is not configured.
/// </summary>
public sealed class BuiltInBpmnEngine : IBpmnEngine
{
    private readonly EcmDbContext _db;
    private readonly ILogger<BuiltInBpmnEngine> _log;

    public BuiltInBpmnEngine(EcmDbContext db, ILogger<BuiltInBpmnEngine> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<string> DeployProcessAsync(string bpmnXml,
        string processName, CancellationToken ct)
    {
        // Store BPMN XML in database for built-in execution
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "BpmnDefinitions" ("Name","BpmnXml","DeployedAt","IsActive")
            VALUES ({0},{1},NOW(),true)
            ON CONFLICT ("Name") DO UPDATE SET "BpmnXml"={1}, "DeployedAt"=NOW()
            """, processName, bpmnXml, ct);

        _log.LogInformation("BPMN definition stored: {Name}", processName);
        return Guid.NewGuid().ToString();
    }

    public async Task<string> StartProcessAsync(string processDefinitionKey,
        Guid documentId, int startedBy, Dictionary<string, object> variables,
        CancellationToken ct)
    {
        var instanceId = Guid.NewGuid().ToString();
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "BpmnInstances"
                ("InstanceId","ProcessKey","DocumentId","StartedBy","StartedAt","Status")
            VALUES ({0},{1},{2},{3},NOW(),'Running')
            """, instanceId, processDefinitionKey, documentId, startedBy, ct);

        _log.LogInformation("Built-in BPMN started: {Key} → {InstanceId}",
            processDefinitionKey, instanceId);
        return instanceId;
    }

    public Task CompleteTaskAsync(string taskId, string action, string? comment,
        int userId, CancellationToken ct) => _db.Database.ExecuteSqlRawAsync("""
            UPDATE "WorkflowTasks"
            SET "Status"='Completed', "CompletedAt"=NOW(),
                "CompletedById"={1}, "ActionTaken"={2}, "Comment"={3}
            WHERE "TaskId"::text={0}
            """, taskId, userId, action, comment ?? "", ct);

    public async Task<IEnumerable<BpmnTask>> GetUserTasksAsync(int userId,
        CancellationToken ct)
    {
        var tasks = await _db.Set<Darah.ECM.Domain.Entities.WorkflowTask>()
            .Where(t => t.AssignedToUserId == userId &&
                        t.Status == string.Pending)
            .ToListAsync(ct);

        return tasks.Select(t => new BpmnTask(
            t.TaskId.ToString(), t.TaskName, t.InstanceId.ToString(),
            userId.ToString(), t.AssignedAt ?? DateTime.UtcNow,
            t.DueAt, "50", new Dictionary<string, object>()));
    }

    public async Task<ProcessStatus> GetProcessStatusAsync(string instanceId,
        CancellationToken ct)
    {
        if (!int.TryParse(instanceId, out var id))
            return new ProcessStatus(instanceId, "unknown", "notFound",
                DateTime.UtcNow, null, []);

        var instance = await _db.WorkflowInstances
            .FirstOrDefaultAsync(i => i.InstanceId == id, ct);

        if (instance == null)
            return new ProcessStatus(instanceId, "unknown", "notFound",
                DateTime.UtcNow, null, []);

        return new ProcessStatus(
            instanceId, "built-in",
            instance.Status.ToString(),
            instance.StartedAt, instance.CompletedAt, []);
    }
}

/// <summary>
/// Factory: returns Flowable if configured, built-in otherwise.
/// </summary>
public static class BpmnEngineFactory
{
    public static IServiceCollection AddBpmnEngine(
        this IServiceCollection services, IConfiguration config)
    {
        if (!string.IsNullOrEmpty(config["Flowable:BaseUrl"]))
        {
            services.AddHttpClient<IBpmnEngine, FlowableEngine>();
        }
        else
        {
            services.AddScoped<IBpmnEngine, BuiltInBpmnEngine>();
        }
        return services;
    }
}

// ─── Standard BPMN 2.0 Process Templates ─────────────────────────────────────
public static class BpmnTemplates
{
    /// <summary>Document approval process — 2 levels.</summary>
    public static string DocumentApproval(string processKey,
        string firstApproverGroup, string finalApproverGroup) => """
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                     xmlns:flowable="http://flowable.org/bpmn"
                     targetNamespace="http://darah.gov.sa/bpmn">

          <process id="__PROCESS_KEY__" name="Document Approval" isExecutable="true">

            <startEvent id="start" name="Document Submitted"/>

            <sequenceFlow sourceRef="start" targetRef="review1"/>

            <userTask id="review1" name="First Review"
                      flowable:candidateGroups="__FIRST_APPROVER__">
              <documentation>First level review and approval</documentation>
            </userTask>

            <sequenceFlow sourceRef="review1" targetRef="gateway1"/>

            <exclusiveGateway id="gateway1" name="First Review Decision"/>

            <sequenceFlow sourceRef="gateway1" targetRef="review2">
              <conditionExpression>${action == 'approve'}</conditionExpression>
            </sequenceFlow>

            <sequenceFlow sourceRef="gateway1" targetRef="rejected">
              <conditionExpression>${action == 'reject'}</conditionExpression>
            </sequenceFlow>

            <userTask id="review2" name="Final Approval"
                      flowable:candidateGroups="__FINAL_APPROVER__">
              <documentation>Final approval by senior management</documentation>
            </userTask>

            <sequenceFlow sourceRef="review2" targetRef="gateway2"/>

            <exclusiveGateway id="gateway2" name="Final Decision"/>

            <sequenceFlow sourceRef="gateway2" targetRef="approved">
              <conditionExpression>${action == 'approve'}</conditionExpression>
            </sequenceFlow>

            <sequenceFlow sourceRef="gateway2" targetRef="rejected">
              <conditionExpression>${action == 'reject'}</conditionExpression>
            </sequenceFlow>

            <endEvent id="approved" name="Document Approved">
              <terminateEventDefinition/>
            </endEvent>

            <endEvent id="rejected" name="Document Rejected">
              <terminateEventDefinition/>
            </endEvent>

          </process>
        </definitions>
        """.Replace("__PROCESS_KEY__", processKey).Replace("__FIRST_APPROVER__", firstApproverGroup).Replace("__FINAL_APPROVER__", finalApproverGroup);

    /// <summary>Records disposal approval process.</summary>
    public static string RecordsDisposal(string processKey) => """
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                     xmlns:flowable="http://flowable.org/bpmn"
                     targetNamespace="http://darah.gov.sa/bpmn">
          <process id="__PROCESS_KEY__" name="Records Disposal" isExecutable="true">
            <startEvent id="start"/>
            <sequenceFlow sourceRef="start" targetRef="legalReview"/>
            <userTask id="legalReview" name="Legal Review"
                      flowable:candidateGroups="legal-team"/>
            <sequenceFlow sourceRef="legalReview" targetRef="rmApproval"/>
            <userTask id="rmApproval" name="Records Manager Approval"
                      flowable:candidateGroups="records-managers"/>
            <sequenceFlow sourceRef="rmApproval" targetRef="disposal"/>
            <serviceTask id="disposal" name="Execute Disposal"
                         flowable:class="sa.gov.darah.ecm.DisposalDelegate"/>
            <sequenceFlow sourceRef="disposal" targetRef="end"/>
            <endEvent id="end"/>
          </process>
        </definitions>
        """.Replace("__PROCESS_KEY__", processKey);
}
