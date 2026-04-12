using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.Events.Document;
using Darah.ECM.Domain.ValueObjects;

namespace Darah.ECM.Domain.Entities;

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
    public int?     CurrentVersionId   { get; private set; }
    public DocumentStatus      Status         { get; private set; } = DocumentStatus.Draft;
    public ClassificationLevel Classification { get; private set; } = ClassificationLevel.Internal;
    public int?     CheckedOutBy  { get; private set; }
    public DateTime? CheckedOutAt { get; private set; }
    public bool     IsCheckedOut  { get; private set; }
    public bool     IsLegalHold   { get; private set; }
    public DateOnly? RetentionExpiresAt { get; private set; }
    public string?  Keywords      { get; private set; }
    public string?  Summary       { get; private set; }
    public DateOnly? DocumentDate { get; private set; }
    public string   Language      { get; private set; } = "ar";
    public Guid?    PrimaryWorkspaceId { get; private set; }

    private Document() { }

    public static Document Create(
        string titleAr, int documentTypeId, int libraryId, int createdBy,
        string documentNumber, string? titleEn = null, int? folderId = null,
        ClassificationLevel? classification = null, DateOnly? documentDate = null,
        string? keywords = null, string? summary = null, int? retentionPolicyId = null)
    {
        if (string.IsNullOrWhiteSpace(titleAr))       throw new ArgumentException("Title (AR) is required.");
        if (string.IsNullOrWhiteSpace(documentNumber)) throw new ArgumentException("Document number is required.");

        var doc = new Document
        {
            DocumentId        = Guid.NewGuid(),
            DocumentNumber    = documentNumber,
            TitleAr           = titleAr.Trim(),
            TitleEn           = titleEn?.Trim(),
            DocumentTypeId    = documentTypeId,
            LibraryId         = libraryId,
            FolderId          = folderId,
            Classification    = classification ?? ClassificationLevel.Internal,
            Status            = DocumentStatus.Draft,
            DocumentDate      = documentDate,
            Keywords          = keywords?.Trim(),
            Summary           = summary?.Trim(),
            RetentionPolicyId = retentionPolicyId
        };
        doc.SetCreated(createdBy);
        doc.RaiseDomainEvent(new DocumentCreatedEvent(doc.DocumentId, doc.DocumentNumber, doc.TitleAr, createdBy));
        return doc;
    }

    public void CheckOut(int userId)
    {
        if (IsCheckedOut)
            throw new InvalidOperationException($"الوثيقة {DocumentNumber} محجوزة بالفعل بواسطة مستخدم آخر.");
        if (IsLegalHold)
            throw new InvalidOperationException($"الوثيقة {DocumentNumber} خاضعة لتجميد قانوني ولا يمكن سحبها.");
        IsCheckedOut = true;
        CheckedOutBy = userId;
        CheckedOutAt = DateTime.UtcNow;
        SetUpdated(userId);
        RaiseDomainEvent(new DocumentCheckedOutEvent(DocumentId, userId));
    }

    public void CheckIn(int persistedVersionId, int userId)
    {
        if (!IsCheckedOut)
            throw new InvalidOperationException($"الوثيقة {DocumentNumber} غير مسحوبة.");
        if (persistedVersionId <= 0)
            throw new ArgumentException("persistedVersionId must be a real DB-generated ID.");
        IsCheckedOut     = false;
        CheckedOutBy     = null;
        CheckedOutAt     = null;
        CurrentVersionId = persistedVersionId;
        SetUpdated(userId);
        RaiseDomainEvent(new DocumentCheckedInEvent(DocumentId, persistedVersionId, userId));
    }

    public void TransitionStatus(DocumentStatus newStatus, int userId)
    {
        if (!Status.CanTransitionTo(newStatus))
            throw new InvalidOperationException(
                $"لا يمكن الانتقال من '{Status}' إلى '{newStatus}'.");
        Status = newStatus;
        SetUpdated(userId);
        if (newStatus == DocumentStatus.Approved)
            RaiseDomainEvent(new DocumentApprovedEvent(DocumentId, DocumentNumber, userId));
        else if (newStatus == DocumentStatus.Archived)
            RaiseDomainEvent(new DocumentArchivedEvent(DocumentId, userId));
    }

    public void ApplyLegalHold(int userId = 0)
    {
        if (IsCheckedOut)
            throw new InvalidOperationException("يجب إيداع الوثيقة قبل تطبيق التجميد القانوني.");
        IsLegalHold = true;
    }

    public void ReleaseLegalHold() => IsLegalHold = false;

    public void AssignRecordClass(int recordClassId, int userId)
    { RecordClassId = recordClassId; SetUpdated(userId); }

    public void SetRetentionExpiry(DateOnly expiry, int userId)
    { RetentionExpiresAt = expiry; SetUpdated(userId); }

    public void SetPrimaryWorkspace(Guid workspaceId, int userId)
    { PrimaryWorkspaceId = workspaceId; SetUpdated(userId); }

    public void UpdateClassification(ClassificationLevel level, int userId)
    { Classification = level; SetUpdated(userId); }

    public void UpdateContent(string titleAr, string? titleEn, string? keywords,
        string? summary, DateOnly? documentDate, int userId)
    {
        TitleAr = titleAr.Trim(); TitleEn = titleEn?.Trim();
        Keywords = keywords?.Trim(); Summary = summary?.Trim();
        DocumentDate = documentDate; SetUpdated(userId);
    }

    public void Move(int? newFolderId, int userId)
    { FolderId = newFolderId; SetUpdated(userId); }

    public bool CanBeDeleted()      => !IsLegalHold && Status != DocumentStatus.Disposed;
    public bool CanSubmitToWorkflow() => !IsCheckedOut && !IsLegalHold &&
        (Status == DocumentStatus.Draft || Status == DocumentStatus.Rejected);
    public bool IsRetentionExpired() => RetentionExpiresAt.HasValue &&
        RetentionExpiresAt.Value <= DateOnly.FromDateTime(DateTime.UtcNow);
}
