using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Moq;
using Xunit;

namespace Darah.ECM.UnitTests.Application.Documents;

public sealed class DeleteDocumentCommandTests
{
    private readonly Mock<IUnitOfWork>   _uow   = new();
    private readonly Mock<ICurrentUser>  _user  = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IDocumentRepository> _docRepo = new();

    private DeleteDocumentCommandHandler CreateHandler()
    {
        _uow.Setup(u => u.Documents).Returns(_docRepo.Object);
        _user.Setup(u => u.UserId).Returns(1);
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);
        return new DeleteDocumentCommandHandler(_uow.Object, _user.Object, _audit.Object);
    }

    [Fact]
    public async Task Handle_DocumentNotFound_ReturnsFail()
    {
        _docRepo.Setup(r => r.GetByGuidAsync(It.IsAny<Guid>(), default))
                .ReturnsAsync((Document?)null);

        var r = await CreateHandler().Handle(new DeleteDocumentCommand(Guid.NewGuid(), null), default);

        Assert.False(r.Success);
        Assert.Contains("غير موجودة", r.Message);
    }

    [Fact]
    public async Task Handle_LegalHoldDoc_ReturnsFail()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.ApplyLegalHold();
        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);

        var r = await CreateHandler().Handle(new DeleteDocumentCommand(doc.DocumentId, null), default);

        Assert.False(r.Success);
        Assert.Contains("تجميد قانوني", r.Message);
    }

    [Fact]
    public async Task Handle_ValidDoc_SoftDeletesAndReturnsSuccess()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-002");
        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);

        var r = await CreateHandler().Handle(new DeleteDocumentCommand(doc.DocumentId, "Test reason"), default);

        Assert.True(r.Success);
        Assert.True(doc.IsDeleted);
        Assert.NotNull(doc.DeletedAt);
    }

    [Fact]
    public async Task Handle_Delete_WritesAuditLog()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-003");
        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);
        _audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        await CreateHandler().Handle(new DeleteDocumentCommand(doc.DocumentId, "reason"), default);

        _audit.Verify(a => a.LogAsync("DocumentDeleted", "Document", doc.DocumentId.ToString(),
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
