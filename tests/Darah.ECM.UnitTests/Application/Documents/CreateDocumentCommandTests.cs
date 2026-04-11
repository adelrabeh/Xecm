using Darah.ECM.Application.Common.Abstractions;
using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Darah.ECM.UnitTests.Application.Documents;

public sealed class CreateDocumentCommandTests
{
    private readonly Mock<IUnitOfWork>              _uow       = new();
    private readonly Mock<ICurrentUser>             _user      = new();
    private readonly Mock<IFileStorageService>      _storage   = new();
    private readonly Mock<IAuditService>            _audit     = new();
    private readonly Mock<IDocumentNumberGenerator> _numbering = new();
    private readonly Mock<IDocumentRepository>      _docRepo   = new();
    private readonly Mock<IDocumentVersionRepository> _verRepo  = new();

    private CreateDocumentCommandHandler CreateHandler()
    {
        _uow.Setup(u => u.Documents).Returns(_docRepo.Object);
        _uow.Setup(u => u.DocumentVersions).Returns(_verRepo.Object);
        _user.Setup(u => u.UserId).Returns(1);
        _user.Setup(u => u.IsAuthenticated).Returns(true);
        _numbering.Setup(n => n.GenerateAsync(It.IsAny<int>(), default))
                  .ReturnsAsync("DOC-2026-00001");
        _storage.Setup(s => s.ProviderName).Returns("TestStorage");
        _storage.Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                  It.IsAny<string>(), default))
                .ReturnsAsync("2026/04/11/test-file.pdf");
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);
        _uow.Setup(u => u.BeginTransactionAsync(default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitTransactionAsync(default)).Returns(Task.CompletedTask);
        return new CreateDocumentCommandHandler(_uow.Object, _user.Object,
            _storage.Object, _audit.Object, _numbering.Object);
    }

    private static FileUploadRequest MakeFile(string name = "test.pdf")
    {
        var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF header
        return new FileUploadRequest(name, "application/pdf", content.Length, content);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        var handler = CreateHandler();
        using var file = MakeFile();
        var cmd = new CreateDocumentCommand
        {
            TitleAr = "وثيقة اختبار", DocumentTypeId = 1, LibraryId = 1,
            ClassificationLevelOrder = 2, File = file
        };

        var result = await handler.Handle(cmd, default);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("DOC-2026-00001", result.Data!.DocumentNumber);
    }

    [Fact]
    public async Task Handle_StoresFile_BeforeDbCommit()
    {
        var storeCallOrder = new List<string>();

        _storage.Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                  It.IsAny<string>(), default))
                .Callback(() => storeCallOrder.Add("store"))
                .ReturnsAsync("path/to/file.pdf");

        _uow.Setup(u => u.CommitAsync(default))
            .Callback(() => storeCallOrder.Add("commit"))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        using var file = MakeFile();
        var cmd = new CreateDocumentCommand
        {
            TitleAr = "Test", DocumentTypeId = 1, LibraryId = 1, File = file
        };

        await handler.Handle(cmd, default);

        Assert.Equal("store", storeCallOrder.First());
    }

    [Fact]
    public async Task Handle_ClassificationApplied_ToDocument()
    {
        Document? capturedDoc = null;
        _docRepo.Setup(r => r.AddAsync(It.IsAny<Document>(), default))
                .Callback<Document, CancellationToken>((d, _) => capturedDoc = d)
                .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        using var file = MakeFile();
        var cmd = new CreateDocumentCommand
        {
            TitleAr = "سري", DocumentTypeId = 1, LibraryId = 1,
            ClassificationLevelOrder = 3, // Confidential
            File = file
        };

        await handler.Handle(cmd, default);

        Assert.NotNull(capturedDoc);
        Assert.Equal(ClassificationLevel.Confidential, capturedDoc!.Classification);
    }

    [Fact]
    public async Task Handle_WorkspaceId_BindsDocument()
    {
        Document? capturedDoc = null;
        _docRepo.Setup(r => r.AddAsync(It.IsAny<Document>(), default))
                .Callback<Document, CancellationToken>((d, _) => capturedDoc = d)
                .Returns(Task.CompletedTask);

        var wsId = Guid.NewGuid();
        var handler = CreateHandler();
        using var file = MakeFile();
        var cmd = new CreateDocumentCommand
        {
            TitleAr = "Test", DocumentTypeId = 1, LibraryId = 1,
            WorkspaceId = wsId, File = file
        };

        await handler.Handle(cmd, default);

        Assert.Equal(wsId, capturedDoc!.PrimaryWorkspaceId);
    }
}
