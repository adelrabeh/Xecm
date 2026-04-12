namespace Darah.ECM.Domain.ValueObjects;

/// <summary>
/// Security classification level value object.
/// Determines download permission, watermark requirement, and relative sensitivity order.
/// </summary>
public sealed class ClassificationLevel : IEquatable<ClassificationLevel>
{
    public static readonly ClassificationLevel Public       = new(1, "PUBLIC",       "عام",       "Public",        allowDownload: true,  requireWatermark: false);
    public static readonly ClassificationLevel Internal     = new(2, "INTERNAL",     "داخلي",     "Internal",      allowDownload: true,  requireWatermark: false);
    public static readonly ClassificationLevel Confidential = new(3, "CONFIDENTIAL", "سري",       "Confidential",  allowDownload: true,  requireWatermark: true);
    public static readonly ClassificationLevel Secret       = new(4, "SECRET",       "سري للغاية","Secret",        allowDownload: false, requireWatermark: true);

    public int    Order            { get; }
    public string Code             { get; }
    public string NameAr           { get; }
    public string NameEn           { get; }
    public bool   AllowDownload    { get; }
    public bool   RequireWatermark { get; }

    private ClassificationLevel(int order, string code, string nameAr, string nameEn,
        bool allowDownload, bool requireWatermark)
    {
        Order = order; Code = code; NameAr = nameAr; NameEn = nameEn;
        AllowDownload = allowDownload; RequireWatermark = requireWatermark;
    }

    private static readonly IReadOnlyList<ClassificationLevel> All =
        new[] { Public, Internal, Confidential, Secret };

    public static ClassificationLevel From(string code)
    {
        var match = All.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ArgumentException($"'{code}' is not a valid ClassificationLevel.");
    }

    public static ClassificationLevel FromCode(string code) => From(code);

    public static ClassificationLevel FromOrder(int order)
    {
        var match = All.FirstOrDefault(c => c.Order == order);
        return match ?? throw new ArgumentException($"No ClassificationLevel with order {order}.");
    }

    public bool IsMoreRestrictiveThan(ClassificationLevel other) => Order > other.Order;

    public bool Equals(ClassificationLevel? other) => other is not null && Code == other.Code;
    public override bool Equals(object? obj) => obj is ClassificationLevel c && Equals(c);
    public override int GetHashCode() => Code.GetHashCode();
    public override string ToString() => Code;
}
