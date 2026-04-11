using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Events.Document;
using Darah.ECM.Domain.ValueObjects;

namespace Darah.ECM.Domain.Entities;

/// <summary>
/// Document entity — the central object in the ECM domain.
/// All state mutations go through explicit methods that enforce business invariants
/// and raise the appropriate domain events.
/// </summary>
public class Document : BaseEntity
{
    public Guid     DocumentId         { get; private set; }
    public string   DocumentNumber     { get; private set; } = string.Empty;
    public string   TitleAr            { get; private set; } = string.Empty;
    public string?  TitleEn            { get; private set; }
    public int      DocumentTypeId     { get; private set; }
    public int      LibraryId          { get; private set; }
    public int?     FolderId           { get; private set; }
    public int?     RecordClassId      { get; private set; }
    public int?     RetentionPolicyId  { get; private set; }

    // ── Version pointer ───────────────────────────────────────
    /// <summary>
    /// FK pointing to the currently active DocumentVersion.
    /// Set by CheckIn() after the version has been persisted and its ID is known.
    /// Never set to 0 or a placeholder — only updated with a real persisted VersionId.
    /// </summary>
    public int?  CurrentVersionId { get; private set; }

    // ── Status & classification ──────────────────────────────
    public DocumentStatus     Status         { get; private set; } = DocumentStatus.Draft;
    public ClassificationLevel Classification { get; private set; } = ClassificationLevel.Internal;

    // ── Check-out ────────────────────────────────────────────
    public int?     CheckedOutBy  { get; private set; }
    public DateTime? CheckedOutAt { get; private set; }
    public bool     IsCheckedOut  { get; private set; }

    // ── Records / Compliance ─────────────────────────────────
    public bool    IsLegalHold        { get; private set; }
    public DateOnly? RetentionExpiresAt { get; private set; }

    // ── Content fields ────────────────────────────────────────
    public string?  Keywords        { get; private set; }
    public string?  Summary         { get; private set; }
    public DateOnly? DocumentDate   { get; private set; }
    public string   Language        { get; private set; } = "ar";

    // ── xECM workspace link ───────────────────────────────────
    public Guid?   PrimaryWorkspaceId { get; private set; }

    // ── EF navigation (private set = DDD convention) ─────────
    public virtual DocumentType       DocumentType { get; private set; } = null!;
    public virtual DocumentLibrary    Library      { get; private set; } = null!;
    public virtual Folder?            Folder       { get; private set; }
    public virtual ICollection<DocumentVersion> Versions      { get; private set; } = new HashSet<DocumentVersion>();
    public virtual ICollection<DocumentTag>     DocumentTags  { get; private set; } = new HashSet<DocumentTag>();
    public virtual ICollection<DocumentComment> Comments      { get; private set; } = new HashSet<DocumentComment>();
    public virtual ICollection<DocumentMetadataValue> MetadataValues { get; private set; } = new HashSet<DocumentMetadataValue>();

    private Document() { }

    // ── Factory ───────────────────────────────────────────────
    public static Document Create(
        string titleAr,
        int documentTypeId,
        int libraryId,
        int createdBy,
        string documentNumber,
        string? titleEn                 = null,
        int? folderId                   = null,
        ClassificationLevel? classification = null,
        DateOnly? documentDate          = null,
        string? keywords                = null,
        string? summary                 = null,
        int? retentionPolicyId          = null)
    {
        if (string.IsNullOrWhiteSpace(titleAr))      throw new ArgumentException("Title (AR) is required.");
        if (string.IsNullOrWhiteSpace(documentNumber)) throw new ArgumentException("Document number is required.");

        var doc = new Document
        {
            DocumentId       = Guid.NewGuid(),
            DocumentNumber   = documentNumber,
            TitleAr          = titleAr.Trim(),
            TitleEn          = titleEn?.Trim(),
            DocumentTypeId   = documentTypeId,
            LibraryId        = libraryId,
            FolderId         = folderId,
            Classification   = classification ?? ClassificationLevel.Internal,
            Status           = DocumentStatus.Draft,
            DocumentDate     = documentDate,
            Keywords         = keywords?.Trim(),
            Summary          = summary?.Trim(),
            RetentionPolicyId = retentionPolicyId
        };
        doc.SetCreated(createdBy);
        doc.RaiseDomainEvent(new DocumentCreatedEvent(
            doc.DocumentId, doc.DocumentNumber, doc.TitleAr, createdBy));
        return doc;
    }

    // ── Check-out / Check-in ──────────────────────────────────
    public void CheckOut(int userId)
    {
        if (IsCheckedOut)
            throw new InvalidOperationException(
                $"Document {DocumentNumber} is already checked out by user {CheckedOutBy}.");
        if (IsLegalHold)
            throw new InvalidOperationException(
                $"Document {DocumentNumber} is under legal hold and cannot be checked out.");

        IsCheckedOut = true;
        CheckedOutBy = userId;
        CheckedOutAt = DateTime.UtcNow;
        SetUpdated(userId);
        RaiseDomainEvent(new DocumentCheckedOutEvent(DocumentId, userId));
    }

    /// <summary>
    /// Releases the check-out lock and sets <see cref="CurrentVersionId"/> to the
    /// persisted version ID. Call only after the DocumentVersion has been saved
    /// and its database-generated ID is available.
    /// </summary>
    public void CheckIn(int persistedVersionId, int userId)
    {
        if (!IsCheckedOut)
            throw new InvalidOperationException(
                $"Document {DocumentNumber} is not currently checked out.");
        if (persistedVersionId <= 0)
            throw new ArgumentException(
                "persistedVersionId must be the real database-generated ID, not a placeholder.");

        IsCheckedOut    = false;
        CheckedOutBy    = null;
        CheckedOutAt    = null;
        CurrentVersionId = persistedVersionId;
        SetUpdated(userId);
        RaiseDomainEvent(new DocumentCheckedInEvent(DocumentId, persistedVersionId, userId));
    }

    // ── Status transition ─────────────────────────────────────
    public void TransitionStatus(DocumentStatus newStatus, int userId)
    {
        if (!Status.CanTransitionTo(newStatus))
            throw new InvalidOperationException(
                $"Cannot transition document {DocumentNumber} from '{Status}' to '{newStatus}'.");

        Status = newStatus;
        SetUpdated(userId);

        if (newStatus == DocumentStatus.Approved)
            RaiseDomainEvent(new DocumentApprovedEvent(DocumentId, DocumentNumber, userId));
        else if (newStatus == DocumentStatus.Archived)
            RaiseDomainEvent(new DocumentArchivedEvent(DocumentId, userId));
    }

    // ── Legal hold ────────────────────────────────────────────
    public void ApplyLegalHold(int appliedBy)
    {
        IsLegalHold = true;
        RaiseDomainEvent(new LegalHoldAppliedEvent(DocumentId, appliedBy));
    }

    public void ReleaseLegalHold() => IsLegalHold = false;

    // ── Content / Metadata updates ────────────────────────────
    public void UpdateContent(string titleAr, string? titleEn,
        string? keywords, string? summary, DateOnly? documentDate, int userId)
    {
        TitleAr      = titleAr.Trim();
        TitleEn      = titleEn?.Trim();
        Keywords     = keywords?.Trim();
        Summary      = summary?.Trim();
        DocumentDate = documentDate;
        SetUpdated(userId);
    }

    public void UpdateClassification(ClassificationLevel level, int userId)
    {
        Classification = level;
        SetUpdated(userId);
    }

    public void SetRetentionExpiry(DateOnly expiry, int userId)
    {
        RetentionExpiresAt = expiry;
        SetUpdated(userId);
    }

    public void SetPrimaryWorkspace(Guid workspaceId, int userId)
    {
        PrimaryWorkspaceId = workspaceId;
        SetUpdated(userId);
    }

    public void Move(int? newFolderId, int userId)
    {
        FolderId = newFolderId;
        SetUpdated(userId);
    }

    // ── Business queries ──────────────────────────────────────
    public bool IsRetentionExpired()
        => RetentionExpiresAt.HasValue
           && RetentionExpiresAt.Value <= DateOnly.FromDateTime(DateTime.UtcNow);

    public bool CanBeSubmittedToWorkflow()
        => !IsCheckedOut
           && !IsLegalHold
           && (Status == DocumentStatus.Draft || Status == DocumentStatus.Rejected);
}
