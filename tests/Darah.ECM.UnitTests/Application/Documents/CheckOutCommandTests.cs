using Darah.ECM.Application.Documents.Commands;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Moq;
using Xunit;

namespace Darah.ECM.UnitTests.Application.Documents;

public sealed class CheckOutCommandTests
{
    private readonly Mock<IUnitOfWork>   _uow   = new();
    private readonly Mock<ICurrentUser>  _user  = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IDocumentRepository> _docRepo = new();

    private CheckOutDocumentCommandHandler CreateHandler()
    {
        _uow.Setup(u => u.Documents).Returns(_docRepo.Object);
        _user.Setup(u => u.UserId).Returns(7);
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);
        return new CheckOutDocumentCommandHandler(_uow.Object, _user.Object, _audit.Object);
    }

    [Fact]
    public async Task Handle_DocumentNotFound_ReturnsFail()
    {
        _docRepo.Setup(r => r.GetByGuidAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Document?)null);
        var r = await CreateHandler().Handle(new CheckOutDocumentCommand(Guid.NewGuid()), default);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Handle_AlreadyCheckedOut_ReturnsFail()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-001");
        doc.CheckOut(1);
        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);
        var r = await CreateHandler().Handle(new CheckOutDocumentCommand(doc.DocumentId), default);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Handle_ValidDoc_SetsCheckedOut()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-002");
        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);
        var r = await CreateHandler().Handle(new CheckOutDocumentCommand(doc.DocumentId), default);
        Assert.True(r.Success);
        Assert.True(doc.IsCheckedOut);
        Assert.Equal(7, doc.CheckedOutBy);
    }

    [Fact]
    public async Task Handle_LegalHoldDoc_ReturnsFail()
    {
        var doc = Document.Create("Test", 1, 1, 1, "DOC-003");
        doc.ApplyLegalHold();
        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);
        var r = await CreateHandler().Handle(new CheckOutDocumentCommand(doc.DocumentId), default);
        Assert.False(r.Success);
        Assert.Contains("تجميد قانوني", r.Message);
    }
}
