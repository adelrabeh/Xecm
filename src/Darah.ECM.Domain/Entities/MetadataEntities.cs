using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Entities;

// ─── METADATA FIELD DEFINITION ────────────────────────────────────────────────
/// <summary>
/// Dynamic metadata field definition.
/// Administrators define fields without code changes.
/// Supports: Text, Number, Date, Boolean, Lookup, MultiValue, LongText, Email, Url.
/// </summary>
public sealed class MetadataField : BaseEntity
{
    public int     FieldId              { get; private set; }
    public string  FieldCode            { get; private set; } = string.Empty;
    public string  LabelAr              { get; private set; } = string.Empty;
    public string  LabelEn              { get; private set; } = string.Empty;

    /// <summary>Text|Number|Date|Boolean|Lookup|MultiValue|LongText|Email|Url|RichText</summary>
    public string  FieldType            { get; private set; } = string.Empty;

    public bool    IsRequired           { get; private set; }
    public bool    IsSearchable         { get; private set; } = true;
    public bool    IsMultiValue         { get; private set; }
    public string? DefaultValue         { get; private set; }
    public string? ValidationRegex      { get; private set; }
    public string? MinValue             { get; private set; }
    public string? MaxValue             { get; private set; }
    public int?    MaxLength            { get; private set; }
    public int?    LookupCategoryId     { get; private set; }
    public string? PlaceholderAr        { get; private set; }
    public string? PlaceholderEn        { get; private set; }
    public string? HelpTextAr           { get; private set; }
    public string? HelpTextEn           { get; private set; }
    public int     SortOrder            { get; private set; }
    public bool    IsSystem             { get; private set; }
    public bool    IsActive             { get; private set; } = true;

    private static readonly IReadOnlySet<string> ValidTypes = new HashSet<string>
    {
        "Text", "Number", "Date", "Boolean", "Lookup",
        "MultiValue", "LongText", "Email", "Url", "RichText"
    };

    private MetadataField() { }

    public static MetadataField Create(string fieldCode, string labelAr, string labelEn,
        string fieldType, int createdBy,
        bool isRequired = false, bool isSearchable = true,
        int? lookupCategoryId = null, string? validationRegex = null,
        int sortOrder = 0, int? maxLength = null)
    {
        if (!ValidTypes.Contains(fieldType))
            throw new ArgumentException($"Invalid field type: {fieldType}. Valid: {string.Join(", ", ValidTypes)}");

        var field = new MetadataField
        {
            FieldCode         = fieldCode.Trim().ToLowerInvariant().Replace(" ", "_"),
            LabelAr           = labelAr,
            LabelEn           = labelEn,
            FieldType         = fieldType,
            IsRequired        = isRequired,
            IsSearchable      = isSearchable,
            LookupCategoryId  = lookupCategoryId,
            ValidationRegex   = validationRegex,
            SortOrder         = sortOrder,
            MaxLength         = maxLength
        };
        field.SetCreated(createdBy);
        return field;
    }

    public void Update(string labelAr, string labelEn, bool isRequired,
        string? helpTextAr, string? helpTextEn, int updatedBy)
    {
        LabelAr    = labelAr;
        LabelEn    = labelEn;
        IsRequired = isRequired;
        HelpTextAr = helpTextAr;
        HelpTextEn = helpTextEn;
        SetUpdated(updatedBy);
    }

    public void Deactivate(int updatedBy) { IsActive = false; SetUpdated(updatedBy); }

    /// <summary>Validates a raw string value against the field's rules.</summary>
    public (bool IsValid, string? Error) Validate(string? value)
    {
        if (IsRequired && string.IsNullOrWhiteSpace(value))
            return (false, $"الحقل '{LabelAr}' إلزامي");

        if (string.IsNullOrWhiteSpace(value)) return (true, null);

        if (MaxLength.HasValue && value.Length > MaxLength.Value)
            return (false, $"الحقل '{LabelAr}' يتجاوز الحد الأقصى ({MaxLength} حرف)");

        if (!string.IsNullOrEmpty(ValidationRegex)
            && !System.Text.RegularExpressions.Regex.IsMatch(value, ValidationRegex))
            return (false, $"الحقل '{LabelAr}' لا يطابق النمط المطلوب");

        return FieldType switch
        {
            "Number" => decimal.TryParse(value, out _)
                ? (true, null) : (false, $"'{LabelAr}' يجب أن يكون رقماً"),
            "Date"   => DateOnly.TryParse(value, out _)
                ? (true, null) : (false, $"'{LabelAr}' يجب أن يكون تاريخاً صحيحاً"),
            "Email"  => value.Contains('@')
                ? (true, null) : (false, $"'{LabelAr}' يجب أن يكون بريداً إلكترونياً صحيحاً"),
            "Url"    => Uri.TryCreate(value, UriKind.Absolute, out _)
                ? (true, null) : (false, $"'{LabelAr}' يجب أن يكون رابطاً صحيحاً"),
            _        => (true, null)
        };
    }
}

// ─── DOCUMENT TYPE METADATA FIELD ─────────────────────────────────────────────
/// <summary>Associates MetadataFields with DocumentTypes (with optional overrides).</summary>
public sealed class DocumentTypeMetadataField
{
    public int   Id                  { get; set; }
    public int   DocumentTypeId      { get; set; }
    public int   FieldId             { get; set; }
    public bool? IsRequiredOverride  { get; set; }   // null = use field default
    public int?  SortOrderOverride   { get; set; }
    public string? GroupName         { get; set; }
    public bool  IsActive            { get; set; } = true;
}

// ─── DOCUMENT METADATA VALUE (EAV) ───────────────────────────────────────────
/// <summary>
/// Stores the actual metadata value for a document using EAV pattern.
/// Typed columns (TextValue, NumberValue, DateValue, BoolValue, LookupValueId)
/// allow efficient indexed queries per field type.
/// </summary>
public sealed class DocumentMetadataValue
{
    public long    ValueId       { get; set; }
    public Guid    DocumentId    { get; set; }
    public int     FieldId       { get; set; }
    public string? TextValue     { get; set; }
    public decimal? NumberValue  { get; set; }
    public DateTime? DateValue   { get; set; }
    public bool?   BoolValue     { get; set; }
    public int?    LookupValueId { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt   { get; set; }

    /// <summary>Returns the stored value as a displayable string.</summary>
    public string? GetDisplayValue() =>
        TextValue     ?? NumberValue?.ToString()
        ?? DateValue?.ToString("yyyy-MM-dd")
        ?? BoolValue?.ToString()
        ?? LookupValueId?.ToString();

    /// <summary>Sets the appropriate typed column based on the field type.</summary>
    public void SetValue(string fieldType, string? rawValue)
    {
        TextValue     = null;
        NumberValue   = null;
        DateValue     = null;
        BoolValue     = null;
        LookupValueId = null;
        UpdatedAt     = DateTime.UtcNow;

        if (rawValue is null) return;

        switch (fieldType)
        {
            case "Number":
                NumberValue = decimal.TryParse(rawValue, out var n) ? n : null; break;
            case "Date":
                DateValue = DateTime.TryParse(rawValue, out var d) ? d : null; break;
            case "Boolean":
                BoolValue = bool.TryParse(rawValue, out var b) ? b : null; break;
            case "Lookup":
                LookupValueId = int.TryParse(rawValue, out var lv) ? lv : null; break;
            default:
                TextValue = rawValue; break;
        }
    }
}
