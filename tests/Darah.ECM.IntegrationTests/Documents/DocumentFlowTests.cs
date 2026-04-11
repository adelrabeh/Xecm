using Darah.ECM.Application.Common.Abstractions;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Darah.ECM.IntegrationTests.Documents;

/// <summary>
/// Integration tests for the Document creation and versioning flow.
/// Tests the full handler chain including transaction management.
///
/// These tests use mocked infrastructure but exercise the real Application handlers
/// and Domain logic end-to-end — verifying the complete orchestration flow.
/// </summary>
public sealed class DocumentCreationFlowTests
{
    private readonly Mock<IUnitOfWork>              _uow          = new();
    private readonly Mock<ICurrentUser>             _user         = new();
    private readonly Mock<IFileStorageService>      _storage      = new();
    private readonly Mock<IFileValidationService>   _validation   = new();
    private readonly Mock<IAuditService>            _audit        = new();
    private readonly Mock<IDocumentNumberGenerator> _numbering    = new();
    private readonly Mock<IDocumentRepository>      _docRepo      = new();
    private readonly Mock<IDocumentVersionRepository> _verRepo    = new();
    private readonly Mock<ILogger<CreateDocumentCommandHandler>> _log = new();

    private CreateDocumentCommandHandler CreateHandler()
    {
        _uow.Setup(u => u.Documents).Returns(_docRepo.Object);
        _uow.Setup(u => u.DocumentVersions).Returns(_verRepo.Object);
        _uow.Setup(u => u.BeginTransactionAsync(default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitTransactionAsync(default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.RollbackTransactionAsync(default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.DispatchDomainEventsAsync(default)).Returns(Task.CompletedTask);

        _user.Setup(u => u.UserId).Returns(1);
        _user.Setup(u => u.IsAuthenticated).Returns(true);

        _numbering.Setup(n => n.GenerateAsync(It.IsAny<int>(), default))
                  .ReturnsAsync("DOC-2026-00001");

        _storage.Setup(s => s.ProviderName).Returns("LocalFileSystem");
        _storage.Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                  It.IsAny<string>(), default))
                .ReturnsAsync("2026/04/11/test.pdf");

        _validation.Setup(v => v.ValidateAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                    It.IsAny<string>(), default))
                   .ReturnsAsync(new FileValidationResult(true, null, "application/pdf"));

        // Simulate CommitAsync incrementing entity IDs
        int commitCall = 0;
        _uow.Setup(u => u.CommitAsync(default))
            .Callback(() => commitCall++)
            .ReturnsAsync(1);

        return new CreateDocumentCommandHandler(
            _uow.Object, _user.Object, _storage.Object,
            _validation.Object, _audit.Object, _numbering.Object, _log.Object);
    }

    private static FileUploadRequest MakePdfUpload()
    {
        var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }); // %PDF-
        return new FileUploadRequest("contract.pdf", "application/pdf", content.Length, content);
    }

    // ── Test 1: Full creation flow succeeds ──────────────────────────────────
    [Fact]
    public async Task CreateDocument_FullFlow_Succeeds()
    {
        using var file = MakePdfUpload();
        var cmd = new CreateDocumentCommand
        {
            TitleAr = "عقد خدمات 2026", DocumentTypeId = 1, LibraryId = 1,
            ClassificationLevelOrder = 2, File = file
        };

        var result = await CreateHandler().Handle(cmd, default);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("DOC-2026-00001", result.Data!.DocumentNumber);
        Assert.Equal("1.0", result.Data.VersionNumber);
        Assert.Equal("INTERNAL", result.Data.ClassificationCode);
    }

    // ── Test 2: Transaction begins before any DB operation ───────────────────
    [Fact]
    public async Task CreateDocument_TransactionBeginsFirst()
    {
        var callOrder = new List<string>();
        _uow.Setup(u => u.BeginTransactionAsync(default))
            .Callback(() => callOrder.Add("begin"))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(default))
            .Callback(() => callOrder.Add("commit"))
            .ReturnsAsync(1);
        _uow.Setup(u => u.CommitTransactionAsync(default))
            .Callback(() => callOrder.Add("commitTx"))
            .Returns(Task.CompletedTask);

        using var file = MakePdfUpload();
        await CreateHandler().Handle(new CreateDocumentCommand
        {
            TitleAr = "Test", DocumentTypeId = 1, LibraryId = 1, File = file
        }, default);

        Assert.Equal("begin", callOrder.First());
        Assert.Equal("commitTx", callOrder.Last());
    }

    // ── Test 3: Events dispatched AFTER transaction commit ───────────────────
    [Fact]
    public async Task CreateDocument_DomainEventsDispatchedAfterCommit()
    {
        var callOrder = new List<string>();
        _uow.Setup(u => u.CommitTransactionAsync(default))
            .Callback(() => callOrder.Add("commitTx")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.DispatchDomainEventsAsync(default))
            .Callback(() => callOrder.Add("dispatch")).Returns(Task.CompletedTask);

        using var file = MakePdfUpload();
        await CreateHandler().Handle(new CreateDocumentCommand
        {
            TitleAr = "Test", DocumentTypeId = 1, LibraryId = 1, File = file
        }, default);

        var commitIdx  = callOrder.IndexOf("commitTx");
        var dispatchIdx = callOrder.IndexOf("dispatch");
        Assert.True(dispatchIdx > commitIdx,
            "Domain events must be dispatched AFTER transaction commit");
    }

    // ── Test 4: File validation failure prevents DB writes ───────────────────
    [Fact]
    public async Task CreateDocument_InvalidFile_NoDbWrites()
    {
        _validation.Setup(v => v.ValidateAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                    It.IsAny<string>(), default))
                   .ReturnsAsync(new FileValidationResult(false, "Executable detected", null));

        using var file = new FileUploadRequest("malware.pdf", "application/pdf", 100,
            new MemoryStream(new byte[] { 0x4D, 0x5A })); // MZ header

        var result = await CreateHandler().Handle(new CreateDocumentCommand
        {
            TitleAr = "Test", DocumentTypeId = 1, LibraryId = 1, File = file
        }, default);

        Assert.False(result.Success);
        // File rejected before any DB operation
        _uow.Verify(u => u.BeginTransactionAsync(default), Times.Never);
        _docRepo.Verify(r => r.AddAsync(It.IsAny<Document>(), default), Times.Never);
    }

    // ── Test 5: Rollback on DB failure ────────────────────────────────────────
    [Fact]
    public async Task CreateDocument_DbFailure_Rollback()
    {
        _uow.Setup(u => u.CommitAsync(default)).ThrowsAsync(new Exception("DB connection lost"));

        using var file = MakePdfUpload();
        var result = await CreateHandler().Handle(new CreateDocumentCommand
        {
            TitleAr = "Test", DocumentTypeId = 1, LibraryId = 1, File = file
        }, default);

        Assert.False(result.Success);
        _uow.Verify(u => u.RollbackTransactionAsync(default), Times.Once);
    }

    // ── Test 6: Workspace binding applied before first commit ─────────────────
    [Fact]
    public async Task CreateDocument_WorkspaceId_BindingApplied()
    {
        Document? capturedDoc = null;
        _docRepo.Setup(r => r.AddAsync(It.IsAny<Document>(), default))
                .Callback<Document, CancellationToken>((d, _) => capturedDoc = d)
                .Returns(Task.CompletedTask);

        var wsId = Guid.NewGuid();
        using var file = MakePdfUpload();
        await CreateHandler().Handle(new CreateDocumentCommand
        {
            TitleAr = "Test", DocumentTypeId = 1, LibraryId = 1,
            WorkspaceId = wsId, File = file
        }, default);

        Assert.Equal(wsId, capturedDoc?.PrimaryWorkspaceId);
    }
}

// ─── LEGAL HOLD ENFORCEMENT TESTS ────────────────────────────────────────────
public sealed class LegalHoldEnforcementTests
{
    [Fact]
    public void Document_CheckOut_UnderLegalHold_Throws()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.ApplyLegalHold();
        var ex = Assert.Throws<InvalidOperationException>(() => doc.CheckOut(1));
        Assert.Contains("تجميد قانوني", ex.Message);
    }

    [Fact]
    public void Document_CanBeDeleted_LegalHold_False()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.ApplyLegalHold();
        Assert.False(doc.CanBeDeleted());
    }

    [Fact]
    public void Document_CanSubmitToWorkflow_LegalHold_False()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.ApplyLegalHold();
        Assert.False(doc.CanSubmitToWorkflow());
    }

    [Fact]
    public void Document_ReleaseLegalHold_RestoresAccess()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.ApplyLegalHold();
        doc.ReleaseLegalHold();
        Assert.True(doc.CanSubmitToWorkflow());
    }
}

// ─── PERMISSION INHERITANCE TESTS ─────────────────────────────────────────────
public sealed class PermissionInheritanceTests
{
    [Fact]
    public void ClassificationLevel_Secret_BlocksDownload()
    {
        var secret = ClassificationLevel.Secret;
        Assert.False(secret.AllowDownload);
        Assert.True(secret.RequireWatermark);
    }

    [Fact]
    public void ClassificationLevel_Confidential_RequiresWatermark()
    {
        var conf = ClassificationLevel.Confidential;
        Assert.True(conf.RequireWatermark);
        Assert.True(conf.AllowDownload);
    }

    [Fact]
    public void DocumentStatus_CannotTransition_FromDisposed()
    {
        var disposed = DocumentStatus.Disposed;
        Assert.False(disposed.CanTransitionTo(DocumentStatus.Active));
        Assert.False(disposed.CanTransitionTo(DocumentStatus.Draft));
    }
}
