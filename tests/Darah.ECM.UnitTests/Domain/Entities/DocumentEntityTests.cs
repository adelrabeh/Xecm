using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Events.Document;
using Darah.ECM.Domain.ValueObjects;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.Entities;

public sealed class DocumentEntityTests
{
    private static Document MakeDoc(string number = "DOC-001")
        => Document.Create("وثيقة اختبار", 1, 1, 1, number);

    // ── Factory ────────────────────────────────────────────────────
    [Fact]
    public void Create_RaisesDocumentCreatedEvent()
    {
        var doc = MakeDoc();
        Assert.Single(doc.DomainEvents);
        Assert.IsType<DocumentCreatedEvent>(doc.DomainEvents.First());
    }

    [Fact]
    public void Create_DefaultStatus_IsDraft()
        => Assert.Equal(DocumentStatus.Draft, MakeDoc().Status);

    [Fact]
    public void Create_DefaultClassification_IsInternal()
        => Assert.Equal(ClassificationLevel.Internal, MakeDoc().Classification);

    [Fact]
    public void Create_DocumentId_IsNotEmpty()
        => Assert.NotEqual(Guid.Empty, MakeDoc().DocumentId);

    // ── CheckOut ──────────────────────────────────────────────────
    [Fact]
    public void CheckOut_SetsIsCheckedOutTrue()
    {
        var doc = MakeDoc();
        doc.CheckOut(42);
        Assert.True(doc.IsCheckedOut);
        Assert.Equal(42, doc.CheckedOutBy);
        Assert.NotNull(doc.CheckedOutAt);
    }

    [Fact]
    public void CheckOut_AlreadyCheckedOut_Throws()
    {
        var doc = MakeDoc();
        doc.CheckOut(1);
        Assert.Throws<InvalidOperationException>(() => doc.CheckOut(2));
    }

    [Fact]
    public void CheckOut_UnderLegalHold_Throws()
    {
        var doc = MakeDoc();
        doc.ApplyLegalHold();
        Assert.Throws<InvalidOperationException>(() => doc.CheckOut(1));
    }

    // ── CheckIn ───────────────────────────────────────────────────
    [Fact]
    public void CheckIn_ValidVersionId_ClearsCheckOutAndSetsCurrentVersion()
    {
        var doc = MakeDoc();
        doc.CheckOut(1);
        doc.CheckIn(99, 1);
        Assert.False(doc.IsCheckedOut);
        Assert.Null(doc.CheckedOutBy);
        Assert.Equal(99, doc.CurrentVersionId);
    }

    [Fact]
    public void CheckIn_WithPlaceholderZero_Throws()
    {
        var doc = MakeDoc();
        doc.CheckOut(1);
        Assert.Throws<ArgumentException>(() => doc.CheckIn(0, 1));
    }

    [Fact]
    public void CheckIn_WithNegativeVersionId_Throws()
    {
        var doc = MakeDoc();
        doc.CheckOut(1);
        Assert.Throws<ArgumentException>(() => doc.CheckIn(-5, 1));
    }

    [Fact]
    public void CheckIn_NotCheckedOut_Throws()
    {
        var doc = MakeDoc();
        Assert.Throws<InvalidOperationException>(() => doc.CheckIn(1, 1));
    }

    // ── TransitionStatus ──────────────────────────────────────────
    [Fact]
    public void TransitionStatus_ValidPath_Succeeds()
    {
        var doc = MakeDoc();
        doc.TransitionStatus(DocumentStatus.Active, 1);
        Assert.Equal(DocumentStatus.Active, doc.Status);
    }

    [Fact]
    public void TransitionStatus_InvalidPath_Throws()
    {
        var doc = MakeDoc(); // Draft
        Assert.Throws<InvalidOperationException>(
            () => doc.TransitionStatus(DocumentStatus.Disposed, 1));
    }

    [Fact]
    public void TransitionStatus_ToApproved_RaisesApprovedEvent()
    {
        var doc = MakeDoc();
        doc.TransitionStatus(DocumentStatus.Pending, 1);
        doc.TransitionStatus(DocumentStatus.Approved, 1);
        var approvedEvent = doc.DomainEvents.OfType<DocumentApprovedEvent>().FirstOrDefault();
        Assert.NotNull(approvedEvent);
        Assert.Equal(doc.DocumentId, approvedEvent.DocumentId);
    }

    [Fact]
    public void TransitionStatus_ToArchived_RaisesArchivedEvent()
    {
        var doc = MakeDoc();
        doc.TransitionStatus(DocumentStatus.Active, 1);
        doc.TransitionStatus(DocumentStatus.Archived, 1);
        Assert.Contains(doc.DomainEvents, e => e is DocumentArchivedEvent);
    }

    // ── LegalHold ─────────────────────────────────────────────────
    [Fact]
    public void ApplyLegalHold_SetsFlag()
    {
        var doc = MakeDoc();
        doc.ApplyLegalHold();
        Assert.True(doc.IsLegalHold);
    }

    [Fact]
    public void ReleaseLegalHold_ClearsFlag()
    {
        var doc = MakeDoc();
        doc.ApplyLegalHold();
        doc.ReleaseLegalHold();
        Assert.False(doc.IsLegalHold);
    }

    // ── Retention ─────────────────────────────────────────────────
    [Fact]
    public void IsRetentionExpired_PastDate_ReturnsTrue()
    {
        var doc = MakeDoc();
        doc.SetRetentionExpiry(new DateOnly(2000, 1, 1), 1);
        Assert.True(doc.IsRetentionExpired());
    }

    [Fact]
    public void IsRetentionExpired_FutureDate_ReturnsFalse()
    {
        var doc = MakeDoc();
        doc.SetRetentionExpiry(new DateOnly(2099, 1, 1), 1);
        Assert.False(doc.IsRetentionExpired());
    }

    [Fact]
    public void IsRetentionExpired_NoExpiry_ReturnsFalse()
        => Assert.False(MakeDoc().IsRetentionExpired());

    // ── SoftDelete ────────────────────────────────────────────────
    [Fact]
    public void SoftDelete_SetsIsDeletedAndTimestamp()
    {
        var doc = MakeDoc();
        doc.SoftDelete(1);
        Assert.True(doc.IsDeleted);
        Assert.NotNull(doc.DeletedAt);
        Assert.Equal(1, doc.DeletedBy);
    }

    // ── CanBeDeleted ──────────────────────────────────────────────
    [Fact]
    public void CanBeDeleted_LegalHold_ReturnsFalse()
    {
        var doc = MakeDoc();
        doc.ApplyLegalHold();
        Assert.False(doc.CanBeDeleted());
    }

    [Fact]
    public void CanBeDeleted_NormalDoc_ReturnsTrue()
        => Assert.True(MakeDoc().CanBeDeleted());

    // ── CanSubmitToWorkflow ───────────────────────────────────────
    [Fact]
    public void CanSubmitToWorkflow_DraftNotCheckedOut_ReturnsTrue()
        => Assert.True(MakeDoc().CanSubmitToWorkflow());

    [Fact]
    public void CanSubmitToWorkflow_CheckedOut_ReturnsFalse()
    {
        var doc = MakeDoc();
        doc.CheckOut(1);
        Assert.False(doc.CanSubmitToWorkflow());
    }

    // ── Workspace binding ────────────────────────────────────────
    [Fact]
    public void SetPrimaryWorkspace_SetsId()
    {
        var doc = MakeDoc();
        var wsId = Guid.NewGuid();
        doc.SetPrimaryWorkspace(wsId, 1);
        Assert.Equal(wsId, doc.PrimaryWorkspaceId);
    }

    // ── ClearDomainEvents ────────────────────────────────────────
    [Fact]
    public void ClearDomainEvents_RemovesAll()
    {
        var doc = MakeDoc();
        Assert.NotEmpty(doc.DomainEvents);
        doc.ClearDomainEvents();
        Assert.Empty(doc.DomainEvents);
    }
}
