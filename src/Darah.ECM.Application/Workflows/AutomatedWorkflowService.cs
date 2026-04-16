using Darah.ECM.Application.Common.Models;
using MediatR;

namespace Darah.ECM.Application.Workflows;

// ─── Commands ─────────────────────────────────────────────────────────────────

/// <summary>AC4: Trigger document state transition with automated routing</summary>
public sealed record TransitionDocumentCommand(
    Guid DocumentId,
    string TargetState,
    string? Comment,
    bool IsElectronicSignature = false)
    : IRequest<ApiResponse<WorkflowTransitionResultDto>>;

public sealed record WorkflowTransitionResultDto(
    Guid DocumentId,
    string PreviousState,
    string NewState,
    DateTime TransitionedAt,
    IEnumerable<NotifiedStakeholderDto> NotifiedStakeholders);

public sealed record NotifiedStakeholderDto(
    int UserId,
    string Name,
    string Email,
    string Action);

// ─── Automated Routing Rules ───────────────────────────────────────────────────

public static class WorkflowRoutingRules
{
    private static readonly Dictionary<(string From, string To), RoutingRule> Rules = new()
    {
        [("Draft", "InternalReview")] = new(
            RequiresSignature: false,
            AutoNotifyRoles: new[] { "Reviewer", "Manager" },
            Action: "Pending Review"),

        [("InternalReview", "Approved")] = new(
            RequiresSignature: true,
            AutoNotifyRoles: new[] { "Requester", "Director" },
            Action: "Document Approved"),

        [("InternalReview", "Rejected")] = new(
            RequiresSignature: false,
            AutoNotifyRoles: new[] { "Requester" },
            Action: "Document Rejected - Action Required"),

        [("Approved", "Archived")] = new(
            RequiresSignature: false,
            AutoNotifyRoles: new[] { "RecordsManager" },
            Action: "Document Archived"),
    };

    public static RoutingRule? GetRule(string from, string to)
        => Rules.TryGetValue((from, to), out var rule) ? rule : null;

    public static IEnumerable<string> GetValidTransitions(string currentState)
        => Rules.Keys
            .Where(k => k.From == currentState)
            .Select(k => k.To);
}

public sealed record RoutingRule(
    bool RequiresSignature,
    string[] AutoNotifyRoles,
    string Action);
