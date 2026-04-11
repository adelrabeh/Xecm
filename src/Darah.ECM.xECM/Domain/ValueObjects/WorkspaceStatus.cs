namespace Darah.ECM.xECM.Domain.ValueObjects;

/// <summary>
/// Workspace status value object — enforces valid workspace lifecycle transitions.
///
/// LIFECYCLE:
///   Draft → Active → Closed → Archived → Disposed
///                  ↘ Archived (bypass Closed)
///
/// KEY DIFFERENCE FROM DocumentStatus:
///   Workspace is a GOVERNED CONTEXT — its status cascades to bound documents.
///   When a workspace is Closed, new documents cannot be added.
///   When Archived, all bound documents transition to Archived.
///   When Disposed, all bound documents are flagged for disposal review.
///
/// RELATIONSHIP TO LEGAL HOLD:
///   Legal hold suspends all status transitions (cannot Archive or Dispose a held workspace).
///   Legal hold does NOT prevent transitioning to Closed (administrative close).
/// </summary>
public sealed class WorkspaceStatus : IEquatable<WorkspaceStatus>
{
    public static readonly WorkspaceStatus Draft    = new("DRAFT",    1);
    public static readonly WorkspaceStatus Active   = new("ACTIVE",   2);
    public static readonly WorkspaceStatus Closed   = new("CLOSED",   3);
    public static readonly WorkspaceStatus Archived = new("ARCHIVED", 4);
    public static readonly WorkspaceStatus Disposed = new("DISPOSED", 5);

    public string Value { get; }
    public int    Order { get; }

    private static readonly IReadOnlyDictionary<string, WorkspaceStatus[]> AllowedTransitions =
        new Dictionary<string, WorkspaceStatus[]>
        {
            [Draft.Value]    = new[] { Active, Archived },
            [Active.Value]   = new[] { Closed, Archived },
            [Closed.Value]   = new[] { Active, Archived },    // Re-open allowed
            [Archived.Value] = new[] { Disposed },
            [Disposed.Value] = Array.Empty<WorkspaceStatus>() // Terminal
        };

    private static readonly WorkspaceStatus[] All =
        { Draft, Active, Closed, Archived, Disposed };

    private WorkspaceStatus(string value, int order) { Value = value; Order = order; }

    public static WorkspaceStatus From(string value)
        => All.FirstOrDefault(s => s.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException($"'{value}' is not a valid WorkspaceStatus.");

    public bool CanTransitionTo(WorkspaceStatus next)
        => AllowedTransitions.TryGetValue(Value, out var allowed) && allowed.Contains(next);

    /// <summary>True if documents can still be added to the workspace.</summary>
    public bool AllowsNewDocuments => this == Draft || this == Active;

    /// <summary>True if the workspace is in a terminal state.</summary>
    public bool IsTerminal => this == Disposed;

    public bool Equals(WorkspaceStatus? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is WorkspaceStatus s && Equals(s);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;
    public static implicit operator string(WorkspaceStatus s) => s.Value;
}
