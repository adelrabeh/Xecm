using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Events.Workflow;

namespace Darah.ECM.Domain.Entities;

// ─── WORKFLOW DEFINITION ──────────────────────────────────────────────────────
/// <summary>
/// DB-driven workflow template. Administrators configure steps without code changes.
/// Version field allows new versions while old instances finish on the previous version.
/// </summary>
public sealed class WorkflowDefinition : BaseEntity
{
    public int     DefinitionId  { get; private set; }
    public string  Code          { get; private set; } = string.Empty;
    public string  NameAr        { get; private set; } = string.Empty;
    public string? NameEn        { get; private set; }
    public string? Description   { get; private set; }
    public int?    DocumentTypeId { get; private set; }   // null = applies to all types
    public string  TriggerType   { get; private set; } = "Manual"; // Manual|OnUpload|OnStatusChange
    public int     Version       { get; private set; } = 1;
    public bool    IsActive      { get; private set; } = true;
    public bool    IsDefault     { get; private set; } = false;

    private readonly List<WorkflowStep> _steps = new();
    public IReadOnlyCollection<WorkflowStep> Steps => _steps.AsReadOnly();

    private WorkflowDefinition() { }

    public static WorkflowDefinition Create(string code, string nameAr, int createdBy,
        string? nameEn = null, int? documentTypeId = null,
        string triggerType = "Manual", bool isDefault = false)
    {
        var def = new WorkflowDefinition
        {
            Code           = code.Trim().ToUpperInvariant(),
            NameAr         = nameAr,
            NameEn         = nameEn,
            DocumentTypeId = documentTypeId,
            TriggerType    = triggerType,
            IsDefault      = isDefault
        };
        def.SetCreated(createdBy);
        return def;
    }

    public WorkflowStep AddStep(string stepCode, string nameAr, int stepOrder,
        string assigneeType, int createdBy,
        int? slaHours = null, bool isFinal = false)
    {
        var step = WorkflowStep.Create(DefinitionId, stepCode, nameAr,
            stepOrder, assigneeType, createdBy, slaHours, isFinal);
        _steps.Add(step);
        return step;
    }

    public WorkflowStep? GetFirstStep()
        => _steps.Where(s => s.IsFirstStep).OrderBy(s => s.StepOrder).FirstOrDefault()
        ?? _steps.OrderBy(s => s.StepOrder).FirstOrDefault();

    public WorkflowStep? GetNextStep(int currentStepId)
    {
        var current = _steps.FirstOrDefault(s => s.StepId == currentStepId);
        if (current is null) return null;
        return _steps.Where(s => s.StepOrder > current.StepOrder)
                     .OrderBy(s => s.StepOrder)
                     .FirstOrDefault();
    }

    public void Activate()  { IsActive = true; }
    public void Deactivate() { IsActive = false; }
    public void BumpVersion() { Version++; }
}

// ─── WORKFLOW STEP ────────────────────────────────────────────────────────────
/// <summary>Individual step within a workflow definition.</summary>
public sealed class WorkflowStep : BaseEntity
{
    public int     StepId           { get; private set; }
    public int     DefinitionId     { get; private set; }
    public string  StepCode         { get; private set; } = string.Empty;
    public string  NameAr           { get; private set; } = string.Empty;
    public string? NameEn           { get; private set; }
    public int     StepOrder        { get; private set; }

    /// <summary>Approval|Review|Notification|Parallel|Conditional</summary>
    public string  StepType         { get; private set; } = "Approval";

    /// <summary>SpecificUser|Role|Department|Dynamic|Sequential|Parallel</summary>
    public string  AssigneeType     { get; private set; } = string.Empty;
    public int?    AssigneeId       { get; private set; }   // UserId if SpecificUser
    public int?    AssigneeRoleId   { get; private set; }   // RoleId if Role
    public int?    AssigneeDeptId   { get; private set; }   // DeptId if Department
    public string? DynamicFieldCode { get; private set; }   // Metadata field that holds assignee

    public int?    SLAHours         { get; private set; }
    public int?    EscalationHours  { get; private set; }
    public int?    EscalationUserId { get; private set; }

    public bool    AllowReject      { get; private set; } = true;
    public bool    AllowReturn      { get; private set; } = true;
    public bool    AllowDelegate    { get; private set; } = true;
    public bool    RequireComment   { get; private set; } = false;
    public bool    IsFirstStep      { get; private set; } = false;
    public bool    IsFinalStep      { get; private set; } = false;
    public bool    NotifyOnAssign   { get; private set; } = true;
    public string? InstructionAr    { get; private set; }

    private WorkflowStep() { }

    public static WorkflowStep Create(int definitionId, string stepCode, string nameAr,
        int stepOrder, string assigneeType, int createdBy,
        int? slaHours = null, bool isFinal = false,
        string stepType = "Approval")
    {
        var step = new WorkflowStep
        {
            DefinitionId = definitionId,
            StepCode     = stepCode,
            NameAr       = nameAr,
            StepOrder    = stepOrder,
            AssigneeType = assigneeType,
            StepType     = stepType,
            SLAHours     = slaHours,
            IsFinalStep  = isFinal,
            IsFirstStep  = stepOrder == 1
        };
        step.SetCreated(createdBy);
        return step;
    }

    public void SetEscalation(int escalationHours, int escalationUserId)
    {
        EscalationHours  = escalationHours;
        EscalationUserId = escalationUserId;
    }

    public void SetAssignee(string type, int? userId = null,
        int? roleId = null, int? deptId = null, string? dynamicField = null)
    {
        AssigneeType     = type;
        AssigneeId       = userId;
        AssigneeRoleId   = roleId;
        AssigneeDeptId   = deptId;
        DynamicFieldCode = dynamicField;
    }
}

// ─── WORKFLOW CONDITION ───────────────────────────────────────────────────────
/// <summary>Conditional routing rule between workflow steps.</summary>
public sealed class WorkflowCondition
{
    public int     ConditionId     { get; set; }
    public int     StepId          { get; set; }
    public string  FieldCode       { get; set; } = string.Empty;

    /// <summary>Equals|NotEquals|GreaterThan|LessThan|Contains|IsEmpty|IsNotEmpty</summary>
    public string  Operator        { get; set; } = string.Empty;
    public string  ConditionValue  { get; set; } = string.Empty;
    public int     TargetStepId    { get; set; }
    public int     SortOrder       { get; set; } = 0;
}

// ─── WORKFLOW ACTION ──────────────────────────────────────────────────────────
/// <summary>
/// Immutable record of every action taken on a workflow task.
/// Append-only — forms the complete workflow audit trail.
/// </summary>
public sealed class WorkflowAction
{
    public int     ActionId        { get; private set; }
    public int     TaskId          { get; private set; }

    /// <summary>Approve|Reject|Return|Delegate|Escalate|Comment|Reassign</summary>
    public string  ActionType      { get; private set; } = string.Empty;
    public string? Comment         { get; private set; }
    public DateTime ActionAt       { get; private set; } = DateTime.UtcNow;
    public int     ActionBy        { get; private set; }
    public int?    DelegatedToId   { get; private set; }
    public string? ActionByName    { get; private set; } // Denormalized

    private WorkflowAction() { }

    public static WorkflowAction Create(int taskId, string actionType,
        int actionBy, string? comment = null,
        int? delegatedToId = null, string? actionByName = null) => new()
    {
        TaskId        = taskId,
        ActionType    = actionType,
        ActionBy      = actionBy,
        Comment       = comment,
        ActionAt      = DateTime.UtcNow,
        DelegatedToId = delegatedToId,
        ActionByName  = actionByName
    };
}

// ─── WORKFLOW DELEGATION ──────────────────────────────────────────────────────
/// <summary>Temporary authority delegation (e.g., during leave).</summary>
public sealed class WorkflowDelegation : BaseEntity
{
    public int     DelegationId  { get; private set; }
    public int     FromUserId    { get; private set; }
    public int     ToUserId      { get; private set; }
    public DateOnly StartDate    { get; private set; }
    public DateOnly EndDate      { get; private set; }
    public string? Reason        { get; private set; }
    public bool    IsActive      { get; private set; } = true;

    private WorkflowDelegation() { }

    public static WorkflowDelegation Create(int fromUser, int toUser,
        DateOnly start, DateOnly end, int createdBy, string? reason = null)
    {
        if (end < start)
            throw new ArgumentException("EndDate must be after StartDate.");
        if (fromUser == toUser)
            throw new ArgumentException("Cannot delegate to yourself.");

        var del = new WorkflowDelegation
        {
            FromUserId = fromUser,
            ToUserId   = toUser,
            StartDate  = start,
            EndDate    = end,
            Reason     = reason
        };
        del.SetCreated(createdBy);
        return del;
    }

    public bool IsCurrentlyActive()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return IsActive && StartDate <= today && today <= EndDate;
    }

    public void Revoke(int revokedBy) { IsActive = false; SetUpdated(revokedBy); }
}
