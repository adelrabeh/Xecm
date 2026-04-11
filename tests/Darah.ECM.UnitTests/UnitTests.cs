// ============================================================
// DOMAIN UNIT TESTS
// ============================================================
namespace Darah.ECM.UnitTests.Domain;

using Xunit;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.Domain.Events;

public class DocumentStatusTests
{
    [Fact]
    public void From_ValidCode_ReturnsStatus()
    {
        var status = DocumentStatus.From("DRAFT");
        Assert.Equal(DocumentStatus.Draft, status);
    }

    [Fact]
    public void From_InvalidCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => DocumentStatus.From("INVALID"));
    }

    [Theory]
    [InlineData("DRAFT",    "ACTIVE",    true)]
    [InlineData("DRAFT",    "PENDING",   true)]
    [InlineData("DRAFT",    "DISPOSED",  false)]
    [InlineData("APPROVED", "ACTIVE",    true)]
    [InlineData("DISPOSED", "ACTIVE",    false)]
    public void CanTransitionTo_VariousTransitions(string from, string to, bool expected)
    {
        var fromStatus = DocumentStatus.From(from);
        var toStatus = DocumentStatus.From(to);
        Assert.Equal(expected, fromStatus.CanTransitionTo(toStatus));
    }
}

public class ClassificationLevelTests
{
    [Fact]
    public void FromOrder_ReturnsCorrectLevel()
    {
        var level = ClassificationLevel.FromOrder(3);
        Assert.Equal(ClassificationLevel.Confidential, level);
    }

    [Fact]
    public void IsMoreRestrictiveThan_ReturnsCorrect()
    {
        Assert.True(ClassificationLevel.Secret.IsMoreRestrictiveThan(ClassificationLevel.Internal));
        Assert.False(ClassificationLevel.Public.IsMoreRestrictiveThan(ClassificationLevel.Internal));
    }

    [Fact]
    public void Confidential_RequiresWatermark()
    {
        Assert.True(ClassificationLevel.Confidential.RequireWatermark);
        Assert.False(ClassificationLevel.Public.RequireWatermark);
    }
}

public class RetentionPeriodTests
{
    [Fact]
    public void ComputeExpiry_AddYearsToTriggerDate()
    {
        var rp = new RetentionPeriod(5, "CreationDate");
        var trigger = new DateOnly(2020, 1, 1);
        var expiry = rp.ComputeExpiry(trigger);
        Assert.Equal(new DateOnly(2025, 1, 1), expiry);
    }

    [Fact]
    public void IsExpired_OldDocument_ReturnsTrue()
    {
        var rp = new RetentionPeriod(1, "CreationDate");
        var trigger = new DateOnly(2000, 1, 1);
        Assert.True(rp.IsExpired(trigger));
    }

    [Fact]
    public void Permanent_NeverExpires()
    {
        var rp = RetentionPeriod.Permanent;
        Assert.Equal(DateOnly.MaxValue, rp.ComputeExpiry(DateOnly.FromDateTime(DateTime.UtcNow)));
    }
}

public class FileMetadataTests
{
    [Fact]
    public void Create_AllowedExtension_Succeeds()
    {
        var fm = FileMetadata.Create("path/to/file", "report.pdf", "application/pdf",
            1024, "abc123", "LocalFileSystem");
        Assert.Equal(".pdf", fm.FileExtension);
    }

    [Fact]
    public void Create_DisallowedExtension_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            FileMetadata.Create("path", "malware.exe", "application/octet-stream",
                100, "hash", "local"));
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1500, "1.5 KB")]
    [InlineData(2_500_000, "2.4 MB")]
    public void FriendlySize_FormatsCorrectly(long bytes, string expected)
    {
        var fm = FileMetadata.Create("key", "file.pdf", "application/pdf", bytes, "h", "local");
        Assert.Equal(expected, fm.FriendlySize);
    }
}

public class DocumentEntityTests
{
    private static Document CreateTestDocument() =>
        Document.Create("وثيقة اختبار", 1, 1, 1, "DOC-2026-00001");

    [Fact]
    public void Create_RaisesDomainEvent()
    {
        var doc = CreateTestDocument();
        Assert.Single(doc.DomainEvents);
        Assert.IsType<DocumentCreatedEvent>(doc.DomainEvents.First());
    }

    [Fact]
    public void CheckOut_SetsIsCheckedOut()
    {
        var doc = CreateTestDocument();
        doc.CheckOut(1);
        Assert.True(doc.IsCheckedOut);
        Assert.Equal(1, doc.CheckedOutBy);
    }

    [Fact]
    public void CheckOut_AlreadyCheckedOut_Throws()
    {
        var doc = CreateTestDocument();
        doc.CheckOut(1);
        Assert.Throws<InvalidOperationException>(() => doc.CheckOut(2));
    }

    [Fact]
    public void CheckOut_LegalHold_Throws()
    {
        var doc = CreateTestDocument();
        doc.ApplyLegalHold();
        Assert.Throws<InvalidOperationException>(() => doc.CheckOut(1));
    }

    [Fact]
    public void TransitionStatus_ValidTransition_Succeeds()
    {
        var doc = CreateTestDocument();
        doc.TransitionStatus(DocumentStatus.Active, 1);
        Assert.Equal(DocumentStatus.Active, doc.Status);
    }

    [Fact]
    public void TransitionStatus_InvalidTransition_Throws()
    {
        var doc = CreateTestDocument();
        Assert.Throws<InvalidOperationException>(() =>
            doc.TransitionStatus(DocumentStatus.Disposed, 1));
    }

    [Fact]
    public void ApplyLegalHold_SetsFlag()
    {
        var doc = CreateTestDocument();
        doc.ApplyLegalHold();
        Assert.True(doc.IsLegalHold);
    }

    [Fact]
    public void SoftDelete_SetsIsDeleted()
    {
        var doc = CreateTestDocument();
        doc.SoftDelete(1);
        Assert.True(doc.IsDeleted);
        Assert.NotNull(doc.DeletedAt);
    }
}

public class WorkflowTaskTests
{
    [Fact]
    public void Create_WithSLA_SetsDueAt()
    {
        var task = WorkflowTask.Create(1, 1, 42, null, 24);
        Assert.NotNull(task.DueAt);
        Assert.True(task.DueAt > DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithoutSLA_DueAtIsNull()
    {
        var task = WorkflowTask.Create(1, 1, 42, null, null);
        Assert.Null(task.DueAt);
    }

    [Fact]
    public void Complete_SetsStatus()
    {
        var task = WorkflowTask.Create(1, 1, 42, null, null);
        task.Complete(42);
        Assert.Equal("Completed", task.Status);
        Assert.Equal(42, task.CompletedBy);
    }
}

// ============================================================
// APPLICATION UNIT TESTS
// ============================================================
namespace Darah.ECM.UnitTests.Application;

using Xunit;
using Moq;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.Entities;

public class DeleteDocumentCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly Mock<IAuditService> _audit = new();

    private DeleteDocumentCommandHandler CreateHandler()
        => new(_uow.Object, _user.Object, _audit.Object);

    [Fact]
    public async Task Handle_DocumentNotFound_ReturnsFail()
    {
        var mockRepo = new Mock<IDocumentRepository>();
        mockRepo.Setup(r => r.GetByGuidAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Document?)null);
        _uow.Setup(u => u.Documents).Returns(mockRepo.Object);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteDocumentCommand(Guid.NewGuid(), null), default);

        Assert.False(result.Success);
        Assert.Contains("غير موجودة", result.Message);
    }

    [Fact]
    public async Task Handle_DocumentOnLegalHold_ReturnsFail()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.ApplyLegalHold();

        var mockRepo = new Mock<IDocumentRepository>();
        mockRepo.Setup(r => r.GetByGuidAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(doc);
        _uow.Setup(u => u.Documents).Returns(mockRepo.Object);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteDocumentCommand(doc.DocumentId, null), default);

        Assert.False(result.Success);
        Assert.Contains("تجميد قانوني", result.Message);
    }

    [Fact]
    public async Task Handle_ValidDocument_Succeeds()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-002");
        var mockRepo = new Mock<IDocumentRepository>();
        mockRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);
        _uow.Setup(u => u.Documents).Returns(mockRepo.Object);
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);
        _user.Setup(u => u.UserId).Returns(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteDocumentCommand(doc.DocumentId, "Test reason"), default);

        Assert.True(result.Success);
        Assert.True(doc.IsDeleted);
    }
}
