namespace Darah.ECM.Domain.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
// DARAH CONTENT MODEL — Institutional Knowledge Asset Framework
// 3 Layers: Base Type → Specialized Type → Aspects
// ═══════════════════════════════════════════════════════════════════════════════

// ─── 1. ASSET TYPES (Type Registry) ───────────────────────────────────────────

public enum AssetType
{
    // Base
    KnowledgeAsset      = 0,
    // Specialized
    Book                = 1,
    Manuscript          = 2,
    HistoricalDocument  = 3,
    ImageAsset          = 4,
    MapAsset            = 5,
    ResearchPaper       = 6,
    AudioVisualAsset    = 7,
    AdministrativeRecord = 8,
    Periodical          = 9,
    Thesis              = 10,
}

public enum AssetStatus
{
    Draft         = 0,
    UnderReview   = 1,
    Approved      = 2,
    Published     = 3,
    Restricted    = 4,
    Archived      = 5,
    Withdrawn     = 6,
}

public enum DigitizationStatus
{
    NotDigitized    = 0,
    InProgress      = 1,
    Digitized       = 2,
    QualityChecked  = 3,
    Published       = 4,
}

public enum OcrQuality
{
    None            = 0,
    Low             = 1,   // < 70%
    Medium          = 2,   // 70-85%
    High            = 3,   // 85-95%
    Excellent       = 4,   // > 95%
}

public enum AssetConfidentiality
{
    Public          = 0,
    Internal        = 1,
    Confidential    = 2,
    StrictlyConfidential = 3,
}

public enum RetentionSchedule
{
    Permanent       = 0,
    Years25         = 1,
    Years10         = 2,
    Years7          = 3,
    Years5          = 4,
    Years3          = 5,
}

// ─── 2. CORE KNOWLEDGE ASSET (Base Type) ──────────────────────────────────────

/// <summary>
/// darah:knowledgeAsset — Base for ALL Darah content.
/// Every asset inherits these properties regardless of type.
/// </summary>
public sealed class KnowledgeAsset : Common.BaseEntity
{
    // Identity
    public long         AssetId          { get; private set; }
    public string       AssetCode        { get; private set; } = "";  // DARAH-DOC-2026-001
    public AssetType    Type             { get; private set; }
    public string       TypeLabel        => Type.ToString();

    // ── darah:cataloging Aspect ────────────────────────────────────────────────
    public string       TitleAr          { get; private set; } = "";
    public string?      TitleEn          { get; private set; }
    public string?      TitleAlt         { get; private set; }  // Alternative title
    public string?      DescriptionAr    { get; private set; }
    public string?      DescriptionEn    { get; private set; }
    public string       Language         { get; private set; } = "ar";
    public string?      Languages        { get; private set; }  // Comma-separated for multilingual
    public string?      Source           { get; private set; }  // Originating entity
    public string?      Publisher        { get; private set; }
    public string?      Creator          { get; private set; }  // Author/Creator
    public string?      Contributors     { get; private set; }  // JSON array
    public string?      Edition          { get; private set; }
    public int?         ProductionYear   { get; private set; }
    public string?      ProductionPeriod { get; private set; }  // "1350-1380 AH"
    public string?      CallNumber       { get; private set; }  // Shelf/retrieval number
    public int?         PageCount        { get; private set; }
    public string?      Dimensions       { get; private set; }  // "25x35 cm"

    // ── darah:classification Aspect ────────────────────────────────────────────
    public string?      PrimarySubject   { get; private set; }
    public string?      SecondarySubjects{ get; private set; }  // JSON array
    public string?      Keywords         { get; private set; }  // Comma-separated
    public string?      GeographicScope  { get; private set; }  // Region/City
    public string?      TemporalCoverage { get; private set; }  // "1900-1950"
    public string?      Era              { get; private set; }  // "First Saudi State"
    public string?      DcmiType         { get; private set; }  // Dublin Core type
    public string?      DarahCategory    { get; private set; }  // Internal classification tree
    public string?      Tags             { get; private set; }  // Comma-separated

    // ── darah:status Aspect ────────────────────────────────────────────────────
    public AssetStatus  Status           { get; private set; } = AssetStatus.Draft;
    public string?      StatusNote       { get; private set; }
    public int?         ApprovedBy       { get; private set; }
    public DateTime?    ApprovedAt       { get; private set; }
    public int?         ReviewedBy       { get; private set; }
    public DateTime?    ReviewedAt       { get; private set; }
    public string?      QualityScore     { get; private set; }  // 0-100

    // ── darah:rights Aspect ────────────────────────────────────────────────────
    public AssetConfidentiality Confidentiality { get; private set; } = AssetConfidentiality.Internal;
    public string?      RightsStatement  { get; private set; }
    public string?      License          { get; private set; }  // CC-BY, Proprietary, etc.
    public string?      Copyright        { get; private set; }
    public bool         IsPublicAccess   { get; private set; }
    public bool         IsDownloadable   { get; private set; } = true;
    public string?      AccessRestrictions { get; private set; }
    public string?      UseConditions    { get; private set; }

    // ── darah:retention Aspect ─────────────────────────────────────────────────
    public RetentionSchedule Retention   { get; private set; } = RetentionSchedule.Permanent;
    public DateTime?    RetentionExpiry  { get; private set; }
    public bool         IsLegalHold      { get; private set; }
    public string?      DisposalMethod   { get; private set; }
    public string?      ArchivalReference { get; private set; }

    // ── darah:digitization Aspect ──────────────────────────────────────────────
    public DigitizationStatus DigitizationStatus { get; private set; } = DigitizationStatus.NotDigitized;
    public OcrQuality   OcrQuality       { get; private set; } = OcrQuality.None;
    public float?       OcrConfidence    { get; private set; }  // 0.0-1.0
    public string?      ScannerModel     { get; private set; }
    public int?         Resolution       { get; private set; }  // DPI
    public string?      ColorProfile     { get; private set; }  // "24-bit RGB"
    public string?      DigitizedBy      { get; private set; }
    public DateTime?    DigitizedAt      { get; private set; }
    public string?      MasterFileFormat { get; private set; }  // "TIFF", "RAW"
    public long?        FileSizeBytes    { get; private set; }
    public string?      Checksum         { get; private set; }  // MD5/SHA256

    // ── Physical/Digital location ──────────────────────────────────────────────
    public string?      PhysicalLocation { get; private set; }  // "Box 3, Shelf A2"
    public string?      FundName         { get; private set; }  // Archival fund
    public string?      CollectionName   { get; private set; }
    public string?      Repository       { get; private set; }  // "Darah Main Archive"
    public int?         FolderId         { get; private set; }  // ECM folder reference
    public Guid?        DocumentId       { get; private set; }  // ECM document reference

    // ── Specialized metadata (JSON for flexibility) ────────────────────────────
    public string?      SpecializedMetadata { get; private set; }  // JSON per asset type
    public string?      LinkedAssets     { get; private set; }  // JSON array of related IDs
    public string?      ExternalIds      { get; private set; }  // WorldCat, ISBN, etc.
    public string?      TraceId          { get; private set; }  // Immutable audit ID

    private KnowledgeAsset() { }

    public static KnowledgeAsset Create(string titleAr, AssetType type, int createdBy,
        string? titleEn = null, string? source = null)
    {
        var a = new KnowledgeAsset
        {
            TitleAr  = titleAr, TitleEn = titleEn,
            Type     = type, Source = source,
            AssetCode = GenerateCode(type),
            TraceId  = Guid.NewGuid().ToString("N"),
            Status   = AssetStatus.Draft,
        };
        a.SetCreated(createdBy);
        return a;
    }

    private static string GenerateCode(AssetType t)
    {
        var prefix = t switch {
            AssetType.Book              => "BK",
            AssetType.Manuscript        => "MS",
            AssetType.HistoricalDocument=> "HD",
            AssetType.ImageAsset        => "IM",
            AssetType.MapAsset          => "MP",
            AssetType.ResearchPaper     => "RP",
            AssetType.AudioVisualAsset  => "AV",
            AssetType.AdministrativeRecord => "AR",
            AssetType.Periodical        => "PR",
            AssetType.Thesis            => "TH",
            _                           => "KA",
        };
        return $"DARAH-{prefix}-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(10000, 99999)}";
    }

    public void UpdateStatus(AssetStatus status, int userId, string? note = null)
    {
        Status = status; StatusNote = note;
        if (status == AssetStatus.Approved) { ApprovedBy = userId; ApprovedAt = DateTime.UtcNow; }
        if (status == AssetStatus.UnderReview) { ReviewedBy = userId; ReviewedAt = DateTime.UtcNow; }
        SetUpdated(userId);
    }

    public void UpdateDigitization(DigitizationStatus status, OcrQuality quality,
        float? confidence, string? digitizedBy, int userId)
    {
        DigitizationStatus = status; OcrQuality = quality;
        OcrConfidence = confidence; DigitizedBy = digitizedBy;
        DigitizedAt = DateTime.UtcNow;
        SetUpdated(userId);
    }
}
