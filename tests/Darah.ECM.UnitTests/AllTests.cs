using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Events.Document;
using Darah.ECM.Domain.ValueObjects;
using Darah.ECM.xECM.Domain.Entities;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.ValueObjects;

// ─── DOCUMENT STATUS TESTS ────────────────────────────────────────────────────
public sealed class DocumentStatusTests
{
    [Fact]
    public void From_ValidCode_ReturnsStatus()
        => Assert.Equal(DocumentStatus.Draft, DocumentStatus.From("DRAFT"));

    [Fact]
    public void From_CaseInsensitive_ReturnsStatus()
        => Assert.Equal(DocumentStatus.Active, DocumentStatus.From("active"));

    [Fact]
    public void From_InvalidCode_Throws()
        => Assert.Throws<ArgumentException>(() => DocumentStatus.From("INVALID_CODE"));

    [Theory]
    [InlineData("DRAFT",    "ACTIVE",    true)]
    [InlineData("DRAFT",    "PENDING",   true)]
    [InlineData("DRAFT",    "ARCHIVED",  true)]
    [InlineData("DRAFT",    "DISPOSED",  false)]
    [InlineData("DRAFT",    "APPROVED",  false)]
    [InlineData("APPROVED", "ACTIVE",    true)]
    [InlineData("APPROVED", "ARCHIVED",  true)]
    [InlineData("APPROVED", "DRAFT",     false)]
    [InlineData("DISPOSED", "ACTIVE",    false)]
    [InlineData("DISPOSED", "DRAFT",     false)]
    public void CanTransitionTo_MatchesMatrix(string from, string to, bool expected)
        => Assert.Equal(expected,
            DocumentStatus.From(from).CanTransitionTo(DocumentStatus.From(to)));

    [Fact]
    public void ImplicitStringConversion_ReturnsValue()
    {
        string value = DocumentStatus.Approved;
        Assert.Equal("APPROVED", value);
    }
}

// ─── CLASSIFICATION LEVEL TESTS ───────────────────────────────────────────────
public sealed class ClassificationLevelTests
{
    [Fact]
    public void FromOrder_ReturnsCorrectLevel()
        => Assert.Equal(ClassificationLevel.Confidential, ClassificationLevel.FromOrder(3));

    [Fact]
    public void FromOrder_Invalid_Throws()
        => Assert.Throws<ArgumentException>(() => ClassificationLevel.FromOrder(99));

    [Fact]
    public void IsMoreRestrictiveThan_ReturnsCorrect()
    {
        Assert.True(ClassificationLevel.Secret.IsMoreRestrictiveThan(ClassificationLevel.Internal));
        Assert.False(ClassificationLevel.Public.IsMoreRestrictiveThan(ClassificationLevel.Internal));
        Assert.False(ClassificationLevel.Internal.IsMoreRestrictiveThan(ClassificationLevel.Internal));
    }

    [Fact]
    public void Confidential_RequiresWatermark() => Assert.True(ClassificationLevel.Confidential.RequireWatermark);
    [Fact]
    public void Public_DoesNotRequireWatermark() => Assert.False(ClassificationLevel.Public.RequireWatermark);
    [Fact]
    public void Secret_DisallowsDownload() => Assert.False(ClassificationLevel.Secret.AllowDownload);
    [Fact]
    public void Internal_AllowsDownload() => Assert.True(ClassificationLevel.Internal.AllowDownload);
}

// ─── RETENTION PERIOD TESTS ───────────────────────────────────────────────────
public sealed class RetentionPeriodTests
{
    [Fact]
    public void ComputeExpiry_AddsYearsToTriggerDate()
    {
        var rp     = new RetentionPeriod(5, "CreationDate");
        var expiry = rp.ComputeExpiry(new DateOnly(2020, 1, 1));
        Assert.Equal(new DateOnly(2025, 1, 1), expiry);
    }

    [Fact]
    public void Permanent_NeverExpires()
        => Assert.Equal(DateOnly.MaxValue,
            RetentionPeriod.Permanent.ComputeExpiry(DateOnly.FromDateTime(DateTime.UtcNow)));

    [Fact]
    public void IsExpired_OldDocument_ReturnsTrue()
        => Assert.True(new RetentionPeriod(1, "CreationDate").IsExpired(new DateOnly(2000, 1, 1)));

    [Fact]
    public void IsExpired_FutureDocument_ReturnsFalse()
        => Assert.False(new RetentionPeriod(10, "CreationDate")
            .IsExpired(DateOnly.FromDateTime(DateTime.UtcNow)));

    [Fact]
    public void Permanent_IsNotExpired()
        => Assert.False(RetentionPeriod.Permanent.IsExpired(new DateOnly(1900, 1, 1)));

    [Fact]
    public void NegativeYears_Throws()
        => Assert.Throws<ArgumentException>(() => new RetentionPeriod(-1, "CreationDate"));
}

// ─── FILE METADATA TESTS ──────────────────────────────────────────────────────
public sealed class FileMetadataTests
{
    [Fact]
    public void Create_AllowedExtension_Succeeds()
    {
        var fm = FileMetadata.Create("key", "report.pdf", "application/pdf", 1024, "hash", "local");
        Assert.Equal(".pdf", fm.FileExtension);
    }

    [Fact]
    public void Create_DisallowedExtension_Throws()
        => Assert.Throws<ArgumentException>(() =>
            FileMetadata.Create("key", "virus.exe", "application/octet-stream", 100, "h", "local"));

    [Fact]
    public void Create_EmptyKey_Throws()
        => Assert.Throws<ArgumentException>(() =>
            FileMetadata.Create("", "file.pdf", "application/pdf", 100, "h", "local"));

    [Theory]
    [InlineData(512,           "512 B")]
    [InlineData(2_000,         "2.0 KB")]
    [InlineData(2_500_000,     "2.4 MB")]
    [InlineData(2_000_000_000, "1.86 GB")]
    public void FriendlySize_FormatsCorrectly(long bytes, string expected)
    {
        var fm = FileMetadata.Create("k", "f.pdf", "application/pdf", bytes, "h", "local");
        Assert.Equal(expected, fm.FriendlySize);
    }

    [Fact]
    public void IsPdf_ReturnsTrue_ForPdf()
    {
        var fm = FileMetadata.Create("k", "f.pdf", "application/pdf", 100, "h", "local");
        Assert.True(fm.IsPdf);
    }
}

namespace Darah.ECM.UnitTests.Domain.Entities;

// ─── DOCUMENT ENTITY TESTS ────────────────────────────────────────────────────
public sealed class DocumentEntityTests
{
    private static Document MakeDocument(string number = "DOC-001")
        => Document.Create("وثيقة اختبار", 1, 1, 1, number);

    [Fact]
    public void Create_SetsInitialStateToDraft()
    {
        var doc = MakeDocument();
        Assert.Equal(DocumentStatus.Draft, doc.Status);
        Assert.False(doc.IsCheckedOut);
        Assert.False(doc.IsLegalHold);
    }

    [Fact]
    public void Create_RaisesDocumentCreatedEvent()
    {
        var doc = MakeDocument();
        Assert.Single(doc.DomainEvents);
        Assert.IsType<DocumentCreatedEvent>(doc.DomainEvents.First());
    }

    [Fact]
    public void CheckOut_SetsIsCheckedOut()
    {
        var doc = MakeDocument();
        doc.CheckOut(1);
        Assert.True(doc.IsCheckedOut);
        Assert.Equal(1, doc.CheckedOutBy);
        Assert.NotNull(doc.CheckedOutAt);
    }

    [Fact]
    public void CheckOut_AlreadyCheckedOut_Throws()
    {
        var doc = MakeDocument();
        doc.CheckOut(1);
        Assert.Throws<InvalidOperationException>(() => doc.CheckOut(2));
    }

    [Fact]
    public void CheckOut_UnderLegalHold_Throws()
    {
        var doc = MakeDocument();
        doc.ApplyLegalHold();
        Assert.Throws<InvalidOperationException>(() => doc.CheckOut(1));
    }

    [Fact]
    public void CheckIn_WithValidVersionId_UpdatesCurrentVersion()
    {
        var doc = MakeDocument();
        doc.CheckOut(1);
        doc.CheckIn(42, 1);
        Assert.False(doc.IsCheckedOut);
        Assert.Equal(42, doc.CurrentVersionId);
        Assert.Null(doc.CheckedOutBy);
    }

    [Fact]
    public void CheckIn_WithZeroVersionId_Throws()
    {
        var doc = MakeDocument();
        doc.CheckOut(1);
        Assert.Throws<ArgumentException>(() => doc.CheckIn(0, 1));
    }

    [Fact]
    public void CheckIn_NotCheckedOut_Throws()
    {
        var doc = MakeDocument();
        Assert.Throws<InvalidOperationException>(() => doc.CheckIn(1, 1));
    }

    [Fact]
    public void TransitionStatus_ValidPath_Succeeds()
    {
        var doc = MakeDocument();
        doc.TransitionStatus(DocumentStatus.Active, 1);
        Assert.Equal(DocumentStatus.Active, doc.Status);
    }

    [Fact]
    public void TransitionStatus_Draft_To_Disposed_Throws()
    {
        var doc = MakeDocument();
        Assert.Throws<InvalidOperationException>(()
            => doc.TransitionStatus(DocumentStatus.Disposed, 1));
    }

    [Fact]
    public void TransitionStatus_ToApproved_RaisesApprovedEvent()
    {
        var doc = MakeDocument();
        doc.TransitionStatus(DocumentStatus.Pending, 1);
        doc.ClearDomainEvents();
        doc.TransitionStatus(DocumentStatus.Approved, 1);
        Assert.Contains(doc.DomainEvents, e => e is DocumentApprovedEvent);
    }

    [Fact]
    public void ApplyLegalHold_SetsFlag()
    {
        var doc = MakeDocument();
        doc.ApplyLegalHold();
        Assert.True(doc.IsLegalHold);
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedAndTimestamp()
    {
        var doc = MakeDocument();
        doc.SoftDelete(1);
        Assert.True(doc.IsDeleted);
        Assert.NotNull(doc.DeletedAt);
        Assert.Equal(1, doc.DeletedBy);
    }

    [Fact]
    public void CanBeDeleted_LegalHold_ReturnsFalse()
    {
        var doc = MakeDocument();
        doc.ApplyLegalHold();
        Assert.False(doc.CanBeDeleted());
    }

    [Fact]
    public void CanSubmitToWorkflow_CheckedOut_ReturnsFalse()
    {
        var doc = MakeDocument();
        doc.CheckOut(1);
        Assert.False(doc.CanSubmitToWorkflow());
    }

    [Fact]
    public void IsRetentionExpired_ExpiredDate_ReturnsTrue()
    {
        var doc = MakeDocument();
        doc.SetRetentionExpiry(new DateOnly(2000, 1, 1), 1);
        Assert.True(doc.IsRetentionExpired());
    }

    [Fact]
    public void IsRetentionExpired_FutureDate_ReturnsFalse()
    {
        var doc = MakeDocument();
        doc.SetRetentionExpiry(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5)), 1);
        Assert.False(doc.IsRetentionExpired());
    }
}

namespace Darah.ECM.UnitTests.Domain.Aggregates;

// ─── WORKSPACE AGGREGATE TESTS ────────────────────────────────────────────────
public sealed class WorkspaceEntityTests
{
    private static Workspace MakeWorkspace()
        => Workspace.Create("مشروع اختبار", 1, "PROJECT", 1, 1, "WS-001", 1);

    [Fact]
    public void Create_IsNotBoundToExternal()
    {
        var ws = MakeWorkspace();
        Assert.False(ws.IsBoundToExternal);
    }

    [Fact]
    public void BindToExternal_SetsBoundFlag()
    {
        var ws = MakeWorkspace();
        ws.BindToExternal("SAP_PROD", "WBS-2026-001", "WBSElement", null, 1);
        Assert.True(ws.IsBoundToExternal);
        Assert.Equal("SAP_PROD", ws.ExternalSystemId);
        Assert.Equal("Pending",  ws.SyncStatus);
    }

    [Fact]
    public void BindToExternal_AlreadyBound_Throws()
    {
        var ws = MakeWorkspace();
        ws.BindToExternal("SAP_PROD", "WBS-001", "WBSElement", null, 1);
        Assert.Throws<InvalidOperationException>(()
            => ws.BindToExternal("SF_CRM", "ACC-001", "Account", null, 1));
    }

    [Fact]
    public void RecordSyncSuccess_UpdatesStatus()
    {
        var ws = MakeWorkspace();
        ws.BindToExternal("SAP_PROD", "WBS-001", "WBSElement", null, 1);
        ws.RecordSyncSuccess(DateTime.UtcNow);
        Assert.Equal("Synced", ws.SyncStatus);
        Assert.Null(ws.SyncError);
        Assert.NotNull(ws.LastSyncedAt);
    }

    [Fact]
    public void RecordSyncFailure_SetsErrorState()
    {
        var ws = MakeWorkspace();
        ws.BindToExternal("SAP_PROD", "WBS-001", "WBSElement", null, 1);
        ws.RecordSyncFailure("Connection timeout");
        Assert.Equal("Failed", ws.SyncStatus);
        Assert.Equal("Connection timeout", ws.SyncError);
    }

    [Fact]
    public void ApplyLegalHold_SetsFlag()
    {
        var ws = MakeWorkspace();
        ws.ApplyLegalHold(1);
        Assert.True(ws.IsLegalHold);
    }

    [Fact]
    public void ReleaseLegalHold_ClearsFlag()
    {
        var ws = MakeWorkspace();
        ws.ApplyLegalHold(1);
        ws.ReleaseLegalHold();
        Assert.False(ws.IsLegalHold);
    }

    [Fact]
    public void Archive_SetsArchivedAt()
    {
        var ws = MakeWorkspace();
        ws.Archive(1);
        Assert.NotNull(ws.ArchivedAt);
        Assert.Equal(1, ws.ArchivedBy);
    }
}

namespace Darah.ECM.UnitTests.Application.Documents;

// ─── DOCUMENT COMMAND HANDLER TESTS ─────────────────────────────────────────
using Moq;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;

public sealed class DeleteDocumentCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>   _uow   = new();
    private readonly Mock<ICurrentUser>  _user  = new();
    private readonly Mock<IAuditService> _audit = new();

    private DeleteDocumentCommandHandler CreateHandler()
        => new(_uow.Object, _user.Object, _audit.Object);

    private Mock<IDocumentRepository> SetupDocRepo(Document? doc)
    {
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetByGuidAsync(It.IsAny<Guid>(), default)).ReturnsAsync(doc);
        _uow.Setup(u => u.Documents).Returns(repo.Object);
        return repo;
    }

    [Fact]
    public async Task Handle_DocumentNotFound_ReturnsFail()
    {
        SetupDocRepo(null);
        var result = await CreateHandler().Handle(
            new DeleteDocumentCommand(Guid.NewGuid(), null), default);
        Assert.False(result.Success);
        Assert.Contains("غير موجودة", result.Message);
    }

    [Fact]
    public async Task Handle_DocumentOnLegalHold_ReturnsFail()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.ApplyLegalHold();
        SetupDocRepo(doc);
        var result = await CreateHandler().Handle(
            new DeleteDocumentCommand(doc.DocumentId, null), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Handle_ValidDocument_SoftDeletesAndSucceeds()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-002");
        SetupDocRepo(doc);
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);
        _user.Setup(u => u.UserId).Returns(1);

        var result = await CreateHandler().Handle(
            new DeleteDocumentCommand(doc.DocumentId, "Test reason"), default);

        Assert.True(result.Success);
        Assert.True(doc.IsDeleted);
        Assert.Equal(1, doc.DeletedBy);
    }

    [Fact]
    public async Task Handle_ValidDocument_AuditIsCalled()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-003");
        SetupDocRepo(doc);
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);
        _user.Setup(u => u.UserId).Returns(5);

        await CreateHandler().Handle(new DeleteDocumentCommand(doc.DocumentId, "reason"), default);

        _audit.Verify(a => a.LogAsync(
            "DocumentDeleted", "Document", doc.DocumentId.ToString(),
            null, null, "Info", true, null, "reason", default), Times.Once);
    }
}

public sealed class ApplyLegalHoldCommandHandlerTests
{
    private readonly Mock<IUnitOfWork>   _uow   = new();
    private readonly Mock<ICurrentUser>  _user  = new();
    private readonly Mock<IAuditService> _audit = new();

    [Fact]
    public async Task Handle_ValidDocument_AppliesHold()
    {
        var doc     = Document.Create("Doc", 1, 1, 1, "DOC-LH-001");
        var repo    = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);
        _uow.Setup(u => u.Documents).Returns(repo.Object);
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);

        var handler = new ApplyLegalHoldToDocumentCommandHandler(_uow.Object, _user.Object, _audit.Object);
        var result  = await handler.Handle(new ApplyLegalHoldToDocumentCommand(doc.DocumentId), default);

        Assert.True(result.Success);
        Assert.True(doc.IsLegalHold);
    }

    [Fact]
    public async Task Handle_AfterLegalHold_CheckOutBlocked()
    {
        var doc = Document.Create("Doc", 1, 1, 1, "DOC-LH-002");
        doc.ApplyLegalHold();
        Assert.Throws<InvalidOperationException>(() => doc.CheckOut(1));
    }
}
