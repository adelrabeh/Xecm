namespace Darah.ECM.Domain.ValueObjects;

/// <summary>
/// DocumentStatus value object.
/// Enforces the allowed state transition matrix at the domain boundary.
/// Illegal transitions throw InvalidOperationException before any persistence occurs.
/// </summary>
public sealed class DocumentStatus : IEquatable<DocumentStatus>
{
    public static readonly DocumentStatus Draft      = new("DRAFT");
    public static readonly DocumentStatus Active     = new("ACTIVE");
    public static readonly DocumentStatus Pending    = new("PENDING");
    public static readonly DocumentStatus Approved   = new("APPROVED");
    public static readonly DocumentStatus Rejected   = new("REJECTED");
    public static readonly DocumentStatus Archived   = new("ARCHIVED");
    public static readonly DocumentStatus Superseded = new("SUPERSEDED");
    public static readonly DocumentStatus Disposed   = new("DISPOSED");

    public string Value { get; }

    private static readonly IReadOnlyDictionary<string, DocumentStatus[]> Transitions =
        new Dictionary<string, DocumentStatus[]>
        {
            [Draft.Value]      = new[] { Active, Pending, Archived },
            [Active.Value]     = new[] { Pending, Archived, Superseded },
            [Pending.Value]    = new[] { Approved, Rejected, Active },
            [Approved.Value]   = new[] { Active, Archived, Superseded },
            [Rejected.Value]   = new[] { Draft, Active },
            [Archived.Value]   = new[] { Disposed },
            [Superseded.Value] = new[] { Archived },
            [Disposed.Value]   = Array.Empty<DocumentStatus>()
        };

    private static readonly IReadOnlyList<DocumentStatus> All = new[]
        { Draft, Active, Pending, Approved, Rejected, Archived, Superseded, Disposed };

    private DocumentStatus(string value) => Value = value;

    public static DocumentStatus From(string value)
    {
        var match = All.FirstOrDefault(s =>
            s.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ArgumentException($"'{value}' is not a valid DocumentStatus.");
    }

    public bool CanTransitionTo(DocumentStatus next)
        => Transitions.TryGetValue(Value, out var allowed) && allowed.Contains(next);

    public bool Equals(DocumentStatus? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is DocumentStatus s && Equals(s);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
    public static implicit operator string(DocumentStatus s) => s.Value;
}
