namespace Darah.ECM.Domain.ValueObjects;

/// <summary>
/// Retention period value object encapsulating duration and trigger type.
/// Computing the expiry date is a domain concern — keeps that logic out of application handlers.
/// </summary>
public sealed record RetentionPeriod
{
    public static readonly RetentionPeriod None      = new(0,   "CreationDate");
    public static readonly RetentionPeriod OneYear   = new(1,   "CreationDate");
    public static readonly RetentionPeriod FiveYears = new(5,   "CreationDate");
    public static readonly RetentionPeriod TenYears  = new(10,  "CreationDate");
    public static readonly RetentionPeriod Permanent = new(999, "CreationDate");

    public int    Years       { get; }
    public string TriggerType { get; }  // CreationDate | DocumentDate | LastModified | EventBased

    public RetentionPeriod(int years, string triggerType)
    {
        if (years < 0) throw new ArgumentException("Retention years cannot be negative.");
        Years = years;
        TriggerType = triggerType;
    }

    /// <summary>Computes the calendar date on which this retention period expires.</summary>
    public DateOnly ComputeExpiry(DateOnly triggerDate)
        => Years == 999 ? DateOnly.MaxValue : triggerDate.AddYears(Years);

    public bool IsExpiredAsOf(DateOnly referenceDate, DateOnly triggerDate)
        => ComputeExpiry(triggerDate) < referenceDate;

    public override string ToString()
        => Years == 999 ? "Permanent" : $"{Years} year(s) from {TriggerType}";
}
