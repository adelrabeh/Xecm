using Darah.ECM.Domain.Common;

namespace Darah.ECM.Domain.Entities;

/// <summary>
/// Folder — a named container within a DocumentLibrary.
/// Supports unlimited nesting (Cabinet → Folder → SubFolder → ...).
/// Materialized path stored for efficient breadcrumb and subtree queries.
/// </summary>
public sealed class Folder : BaseEntity
{
    public int     FolderId       { get; private set; }
    public int?    ParentFolderId { get; private set; }
    public int     LibraryId      { get; private set; }
    public string  NameAr         { get; private set; } = string.Empty;
    public string? NameEn         { get; private set; }
    public string? Description    { get; private set; }

    /// <summary>
    /// Materialized path: "/1/5/12/" where each segment is a FolderId.
    /// Enables efficient subtree queries: WHERE Path LIKE '/1/5/%'
    /// </summary>
    public string  Path           { get; private set; } = "/";
    public int     DepthLevel     { get; private set; } = 0;
    public int     SortOrder      { get; private set; } = 0;
    public bool    IsActive       { get; private set; } = true;

    // Navigation (not persisted directly — populated by EF)
    public int DocumentCount => 0; // computed in queries

    private Folder() { }

    public static Folder Create(string nameAr, int libraryId, int createdBy,
        int? parentFolderId = null, string? nameEn = null,
        string? description = null, int sortOrder = 0)
    {
        var folder = new Folder
        {
            NameAr         = nameAr.Trim(),
            NameEn         = nameEn?.Trim(),
            LibraryId      = libraryId,
            ParentFolderId = parentFolderId,
            Description    = description,
            SortOrder      = sortOrder,
            IsActive       = true
        };
        folder.SetCreated(createdBy);
        return folder;
    }

    /// <summary>Set the materialized path after the folder is persisted (FolderId known).</summary>
    public void SetPath(string parentPath, int depth)
    {
        Path       = $"{parentPath.TrimEnd('/')}/{FolderId}/";
        DepthLevel = depth;
    }

    public void Rename(string nameAr, string? nameEn, int updatedBy)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn?.Trim();
        SetUpdated(updatedBy);
    }

    public void Move(int? newParentFolderId, string newPath, int newDepth, int updatedBy)
    {
        ParentFolderId = newParentFolderId;
        Path           = newPath;
        DepthLevel     = newDepth;
        SetUpdated(updatedBy);
    }

    public void Deactivate(int updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public bool IsRootFolder => ParentFolderId is null;

    /// <summary>Returns true if this folder is an ancestor of targetPath.</summary>
    public bool IsAncestorOf(string targetPath)
        => targetPath.StartsWith(Path, StringComparison.Ordinal) && targetPath != Path;
}

/// <summary>DocumentLibrary — top-level container (cabinet) for folders and documents.</summary>
public sealed class DocumentLibrary : BaseEntity
{
    public int     LibraryId         { get; private set; }
    public string  LibraryCode       { get; private set; } = string.Empty;
    public string  NameAr            { get; private set; } = string.Empty;
    public string? NameEn            { get; private set; }
    public string? Description       { get; private set; }
    public int?    DepartmentId      { get; private set; }
    public int?    DefaultDocTypeId  { get; private set; }
    public int?    RetentionPolicyId { get; private set; }
    public decimal? StorageQuotaGB   { get; private set; }
    public bool    IsPublic          { get; private set; }
    public int     SortOrder         { get; private set; }
    public bool    IsActive          { get; private set; } = true;

    private DocumentLibrary() { }

    public static DocumentLibrary Create(string code, string nameAr, int createdBy,
        string? nameEn = null, int? departmentId = null,
        decimal? quotaGb = null, bool isPublic = false)
    {
        var lib = new DocumentLibrary
        {
            LibraryCode  = code.Trim().ToUpperInvariant(),
            NameAr       = nameAr.Trim(),
            NameEn       = nameEn?.Trim(),
            DepartmentId = departmentId,
            StorageQuotaGB = quotaGb,
            IsPublic     = isPublic
        };
        lib.SetCreated(createdBy);
        return lib;
    }

    public void Update(string nameAr, string? nameEn, string? description, int updatedBy)
    {
        NameAr      = nameAr;
        NameEn      = nameEn;
        Description = description;
        SetUpdated(updatedBy);
    }
}

/// <summary>DocumentRelation — parent/child or reference link between two documents.</summary>
public sealed class DocumentRelation : BaseEntity
{
    public int    RelationId        { get; private set; }
    public Guid   SourceDocumentId  { get; private set; }
    public Guid   TargetDocumentId  { get; private set; }

    /// <summary>ParentChild | Reference | Supersedes | RelatedTo | Attachment</summary>
    public string RelationType      { get; private set; } = string.Empty;
    public string? Note             { get; private set; }

    private DocumentRelation() { }

    public static DocumentRelation Create(Guid sourceId, Guid targetId,
        string relationType, int createdBy, string? note = null)
    {
        if (sourceId == targetId)
            throw new ArgumentException("A document cannot be related to itself.");

        var rel = new DocumentRelation
        {
            SourceDocumentId = sourceId,
            TargetDocumentId = targetId,
            RelationType     = relationType,
            Note             = note
        };
        rel.SetCreated(createdBy);
        return rel;
    }
}

/// <summary>CheckoutLock — ensures only one user can edit a document at a time with timeout.</summary>
public sealed class CheckoutLock : BaseEntity
{
    public int      LockId      { get; private set; }
    public Guid     DocumentId  { get; private set; }
    public int      LockedBy    { get; private set; }
    public DateTime LockedAt    { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAt   { get; private set; }
    public bool     IsReleased  { get; private set; }

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromHours(8);

    private CheckoutLock() { }

    public static CheckoutLock Create(Guid documentId, int userId,
        TimeSpan? timeout = null)
    {
        var expiry = DateTime.UtcNow.Add(timeout ?? DefaultTimeout);
        var lock_ = new CheckoutLock
        {
            DocumentId = documentId,
            LockedBy   = userId,
            LockedAt   = DateTime.UtcNow,
            ExpiresAt  = expiry
        };
        lock_.SetCreated(userId);
        return lock_;
    }

    public void Release(int userId)
    {
        IsReleased = true;
        SetUpdated(userId);
    }

    public bool IsExpired()  => DateTime.UtcNow > ExpiresAt;
    public bool IsActive()   => !IsReleased && !IsExpired();
    public bool OwnedBy(int userId) => LockedBy == userId;
}
