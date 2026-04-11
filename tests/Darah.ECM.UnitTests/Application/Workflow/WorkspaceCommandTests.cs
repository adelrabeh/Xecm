using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.xECM.Application.Commands;
using Darah.ECM.xECM.Domain.Entities;
using Darah.ECM.xECM.Domain.Interfaces;
using Moq;
using Xunit;

namespace Darah.ECM.UnitTests.Application.Workflow;

public sealed class WorkspaceCommandTests
{
    private readonly Mock<IWorkspaceRepository> _wsRepo   = new();
    private readonly Mock<ICurrentUser>         _user     = new();
    private readonly Mock<IAuditService>        _audit    = new();
    private readonly Mock<IMetadataSyncEngine>  _sync     = new();
    private readonly Mock<IWorkspaceNumberGenerator> _num  = new();

    private static Workspace MakeWorkspace(bool bound = false)
    {
        var ws = Workspace.Create("Test WS", 1, 1, 1, "WS-001", 1);
        if (bound) ws.BindToExternal("SAP_PROD", "WBS-001", "WBSElement", null, 1);
        return ws;
    }

    // ── BindExternalObject ────────────────────────────────────────
    [Fact]
    public async Task BindExternal_WorkspaceNotFound_ReturnsFail()
    {
        _wsRepo.Setup(r => r.GetByGuidAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Workspace?)null);
        var handler = new BindExternalObjectCommandHandler(_wsRepo.Object, _user.Object, _audit.Object, _sync.Object);
        var r = await handler.Handle(new BindExternalObjectCommand(Guid.NewGuid(), "SAP_PROD", "ID", "Type", null), default);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task BindExternal_AlreadyBound_ReturnsFail()
    {
        var ws = MakeWorkspace(bound: true);
        _wsRepo.Setup(r => r.GetByGuidAsync(ws.WorkspaceId, default)).ReturnsAsync(ws);
        var handler = new BindExternalObjectCommandHandler(_wsRepo.Object, _user.Object, _audit.Object, _sync.Object);
        var r = await handler.Handle(new BindExternalObjectCommand(ws.WorkspaceId, "SF_CRM", "ACC-01", "Account", null), default);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task BindExternal_ValidWorkspace_BindsAndTriggersSyncAsync()
    {
        var ws = MakeWorkspace();
        _wsRepo.Setup(r => r.GetByGuidAsync(ws.WorkspaceId, default)).ReturnsAsync(ws);
        _sync.Setup(s => s.TriggerSyncAsync(It.IsAny<Guid>(), It.IsAny<SyncDirection>(), default))
             .ReturnsAsync(new SyncResult(true, 5, 0));
        _audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            null, It.IsAny<object>(), It.IsAny<string>(), true, null, null, default))
              .Returns(Task.CompletedTask);

        var handler = new BindExternalObjectCommandHandler(_wsRepo.Object, _user.Object, _audit.Object, _sync.Object);
        var r = await handler.Handle(
            new BindExternalObjectCommand(ws.WorkspaceId, "SAP_PROD", "WBS-999", "WBSElement", "http://sap/wbs/999", true),
            default);

        Assert.True(r.Success);
        Assert.True(ws.IsBoundToExternal);
        Assert.Equal("SAP_PROD", ws.ExternalSystemId);
        _sync.Verify(s => s.TriggerSyncAsync(ws.WorkspaceId, SyncDirection.Inbound, default), Times.Once);
    }

    // ── Archive ───────────────────────────────────────────────────
    [Fact]
    public async Task Archive_ValidWorkspace_SetsArchivedAt()
    {
        var ws = MakeWorkspace();
        _wsRepo.Setup(r => r.GetByGuidAsync(ws.WorkspaceId, default)).ReturnsAsync(ws);
        _user.Setup(u => u.UserId).Returns(5);
        _audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            null, null, It.IsAny<string>(), true, null, It.IsAny<string>(), default))
              .Returns(Task.CompletedTask);

        var handler = new ArchiveWorkspaceCommandHandler(_wsRepo.Object, _user.Object, _audit.Object);
        var r = await handler.Handle(new ArchiveWorkspaceCommand(ws.WorkspaceId, "Project closed"), default);

        Assert.True(r.Success);
        Assert.NotNull(ws.ArchivedAt);
        Assert.Equal(5, ws.ArchivedBy);
    }

    // ── ApplyLegalHold ────────────────────────────────────────────
    [Fact]
    public async Task ApplyLegalHold_SetsIsLegalHoldOnWorkspace()
    {
        var ws = MakeWorkspace();
        _wsRepo.Setup(r => r.GetByGuidAsync(ws.WorkspaceId, default)).ReturnsAsync(ws);
        _user.Setup(u => u.UserId).Returns(3);
        _audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            null, null, It.IsAny<string>(), true, null, null, default)).Returns(Task.CompletedTask);

        var handler = new ApplyWorkspaceLegalHoldCommandHandler(_wsRepo.Object, _user.Object, _audit.Object);
        var r = await handler.Handle(new ApplyWorkspaceLegalHoldCommand(ws.WorkspaceId), default);

        Assert.True(r.Success);
        Assert.True(ws.IsLegalHold);
    }

    // ── TriggerSync ───────────────────────────────────────────────
    [Fact]
    public async Task TriggerSync_ValidDirection_ReturnsSyncResult()
    {
        _sync.Setup(s => s.TriggerSyncAsync(It.IsAny<Guid>(), SyncDirection.Inbound, default))
             .ReturnsAsync(new SyncResult(true, 10, 1));

        var handler = new TriggerWorkspaceSyncCommandHandler(_sync.Object);
        var r = await handler.Handle(new TriggerWorkspaceSyncCommand(Guid.NewGuid(), "Inbound"), default);

        Assert.True(r.Success);
        Assert.Equal(10, r.Data!.FieldsUpdated);
        Assert.Equal(1, r.Data.ConflictsDetected);
    }

    [Fact]
    public async Task TriggerSync_InvalidDirection_ReturnsFail()
    {
        var handler = new TriggerWorkspaceSyncCommandHandler(_sync.Object);
        var r = await handler.Handle(new TriggerWorkspaceSyncCommand(Guid.NewGuid(), "INVALID"), default);
        Assert.False(r.Success);
    }
}
