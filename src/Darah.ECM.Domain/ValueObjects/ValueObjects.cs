namespace Darah.ECM.Domain.ValueObjects;

/// <summary>Document status value object — enforces valid transitions.</summary>
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

    private static readonly Dictionary<string, DocumentStatus[]> _allowedTransitions = new()
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

    private DocumentStatus(string value) => Value = value;

    public static DocumentStatus From(string value)
    {
        var all = new[] { Draft, Active, Pending, Approved, Rejected, Archived, Superseded, Disposed };
        return all.FirstOrDefault(s => s.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"'{value}' is not a valid DocumentStatus.");
    }

    public bool CanTransitionTo(DocumentStatus next)
        => _allowedTransitions.TryGetValue(Value, out var allowed) && allowed.Contains(next);

    public bool Equals(DocumentStatus? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is DocumentStatus s && Equals(s);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
    public static implicit operator string(DocumentStatus s) => s.Value;
}

/// <summary>Security classification level value object.</summary>
public sealed class ClassificationLevel : IEquatable<ClassificationLevel>
{
    public static readonly ClassificationLevel Public       = new(1, "PUBLIC",       "عام",      "Public",       false, false);
    public static readonly ClassificationLevel Internal     = new(2, "INTERNAL",     "داخلي",    "Internal",     true,  false);
    public static readonly ClassificationLevel Confidential = new(3, "CONFIDENTIAL", "سري",      "Confidential", true,  true);
    public static readonly ClassificationLevel Secret       = new(4, "SECRET",       "سري للغاية","Secret",      false, false);

    public int    Order        { get; }
    public string Code         { get; }
    public string NameAr       { get; }
    public string NameEn       { get; }
    public bool   AllowDownload { get; }
    public bool   RequireWatermark { get; }

    private ClassificationLevel(int order, string code, string nameAr, string nameEn, bool allowDownload, bool requireWatermark)
    {
        Order = order; Code = code; NameAr = nameAr; NameEn = nameEn;
        AllowDownload = allowDownload; RequireWatermark = requireWatermark;
    }

    public static ClassificationLevel From(string code)
    {
        var all = new[] { Public, Internal, Confidential, Secret };
        return all.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"'{code}' is not a valid ClassificationLevel.");
    }

    public static ClassificationLevel FromOrder(int order)
    {
        var all = new[] { Public, Internal, Confidential, Secret };
        return all.FirstOrDefault(c => c.Order == order)
               ?? throw new ArgumentException($"No ClassificationLevel with order {order}.");
    }

    public bool IsMoreRestrictiveThan(ClassificationLevel other) => Order > other.Order;

    public bool Equals(ClassificationLevel? other) => other is not null && Code == other.Code;
    public override bool Equals(object? obj) => obj is ClassificationLevel c && Equals(c);
    public override int GetHashCode() => Code.GetHashCode();
    public override string ToString() => Code;
}

/// <summary>Retention period value object with business rules.</summary>
public sealed class RetentionPeriod : IEquatable<RetentionPeriod>
{
    public int Years { get; }
    public string TriggerType { get; }  // CreationDate | DocumentDate | LastModified | EventBased

    public static RetentionPeriod None     => new(0,  "CreationDate");
    public static RetentionPeriod OneYear  => new(1,  "CreationDate");
    public static RetentionPeriod FiveYears => new(5, "CreationDate");
    public static RetentionPeriod TenYears  => new(10,"CreationDate");
    public static RetentionPeriod Permanent => new(999,"CreationDate");

    public RetentionPeriod(int years, string triggerType)
    {
        if (years < 0) throw new ArgumentException("Retention years cannot be negative.");
        Years = years;
        TriggerType = triggerType;
    }

    public DateOnly ComputeExpiry(DateOnly triggerDate)
        => Years == 999 ? DateOnly.MaxValue : triggerDate.AddYears(Years);

    public bool IsExpired(DateOnly triggerDate) => ComputeExpiry(triggerDate) <= DateOnly.FromDateTime(DateTime.UtcNow);

    public bool Equals(RetentionPeriod? other) => other is not null && Years == other.Years && TriggerType == other.TriggerType;
    public override bool Equals(object? obj) => obj is RetentionPeriod r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(Years, TriggerType);
    public override string ToString() => Years == 999 ? "Permanent" : $"{Years} year(s) from {TriggerType}";
}

/// <summary>File metadata value object — immutable once created.</summary>
public sealed record FileMetadata(
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    string FileExtension,
    long FileSizeBytes,
    string ContentHash,   // SHA-256
    string StorageProvider)
{
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".txt",
        ".jpg", ".jpeg", ".png", ".tif", ".tiff",
        ".mp4", ".mp3", ".zip", ".csv", ".msg"
    };

    public static FileMetadata Create(string storageKey, string fileName, string contentType,
        long sizeBytes, string hash, string provider)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File extension '{ext}' is not permitted.");
        return new FileMetadata(storageKey, fileName, contentType, ext, sizeBytes, hash, provider);
    }

    public string FriendlySize => FileSizeBytes switch
    {
        < 1024            => $"{FileSizeBytes} B",
        < 1_048_576       => $"{FileSizeBytes / 1024.0:F1} KB",
        < 1_073_741_824   => $"{FileSizeBytes / 1_048_576.0:F1} MB",
        _                 => $"{FileSizeBytes / 1_073_741_824.0:F2} GB"
    };
}

/// <summary>Money value object for cost-related metadata (contracts, purchases).</summary>
public sealed record Money(decimal Amount, string Currency = "SAR")
{
    public static Money Zero => new(0, "SAR");
    public Money Add(Money other)
    {
        if (Currency != other.Currency) throw new InvalidOperationException("Cannot add different currencies.");
        return new Money(Amount + other.Amount, Currency);
    }
    public override string ToString() => $"{Amount:N2} {Currency}";
}
