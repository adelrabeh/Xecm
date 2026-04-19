namespace Darah.ECM.Domain.Entities;

/// <summary>
/// DMS Taxonomy — 3 primary domains, max 3 levels deep.
/// </summary>
public sealed class TaxonomyCategory : Common.BaseEntity
{
    public int     CategoryId   { get; private set; }
    public string  Code         { get; private set; } = "";
    public string  NameAr       { get; private set; } = "";
    public string  NameEn       { get; private set; } = "";
    public string  Domain       { get; private set; } = "Administrative"; // Administrative|Historical|Publications
    public int?    ParentId     { get; private set; }
    public int     Level        { get; private set; } = 1;  // 1=Domain 2=Area 3=Type
    public string? Icon         { get; private set; }
    public bool    IsActive     { get; private set; } = true;
    public bool    IsSystem     { get; private set; } // Cannot be deleted
    public int     SortOrder    { get; private set; }

    private TaxonomyCategory() { }

    public static TaxonomyCategory Create(string code, string nameAr, string nameEn,
        string domain, int createdBy, int? parentId = null, int level = 1,
        string? icon = null, bool isSystem = false)
    {
        var c = new TaxonomyCategory
        {
            Code = code, NameAr = nameAr, NameEn = nameEn,
            Domain = domain, ParentId = parentId, Level = level,
            Icon = icon, IsSystem = isSystem
        };
        c.SetCreated(createdBy);
        return c;
    }
}

/// <summary>Links a document to taxonomy categories (primary + secondary).</summary>
public sealed class DocumentCategory
{
    public int  Id           { get; set; }
    public Guid DocumentId   { get; set; }
    public int  CategoryId   { get; set; }
    public bool IsPrimary    { get; set; } = true;
}
