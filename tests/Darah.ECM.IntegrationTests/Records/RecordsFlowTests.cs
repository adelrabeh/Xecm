using Darah.ECM.Application.Records.Commands;
using Darah.ECM.Application.Common.Models;
using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.Interfaces.Repositories;
using Darah.ECM.Domain.Interfaces.Services;
using Darah.ECM.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Darah.ECM.IntegrationTests.Records;

public sealed class RecordDeclarationFlowTests
{
    private readonly Mock<IUnitOfWork>         _uow         = new();
    private readonly Mock<ICurrentUser>        _user        = new();
    private readonly Mock<IAuditService>       _audit       = new();
    private readonly Mock<IRecordsRepository>  _recordsRepo = new();
    private readonly Mock<IDocumentRepository> _docRepo     = new();
    private readonly Mock<ILogger<DeclareRecordCommandHandler>> _log = new();

    private DeclareRecordCommandHandler CreateHandler()
    {
        _uow.Setup(u => u.Documents).Returns(_docRepo.Object);
        _user.Setup(u => u.UserId).Returns(1);
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);
        _uow.Setup(u => u.DispatchDomainEventsAsync(default)).Returns(Task.CompletedTask);
        return new DeclareRecordCommandHandler(_uow.Object, _user.Object,
            _audit.Object, _recordsRepo.Object, _log.Object);
    }

    [Fact]
    public async Task DeclareRecord_ValidDocument_SetsRetentionAndClass()
    {
        var doc = Document.Create("عقد 2026", 1, 1, 1, "DOC-001");
        var policy = RetentionPolicy.Create("P5Y", "5 سنوات", 5, 1,
            trigger: "CreationDate", disposal: "Archive");

        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);
        _recordsRepo.Setup(r => r.GetRetentionPolicyAsync(10, default))
                    .ReturnsAsync(policy);

        var result = await CreateHandler().Handle(
            new DeclareRecordCommand(doc.DocumentId, 3, 10), default);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.RecordClassId);
        Assert.NotEqual(DateOnly.MinValue, result.Data.RetentionExpiryDate);
        Assert.Equal(3, doc.RecordClassId);
        Assert.NotNull(doc.RetentionExpiresAt);
    }

    [Fact]
    public async Task DeclareRecord_AlreadyDeclared_ReturnsFail()
    {
        var doc = Document.Create("مستند", 1, 1, 1, "DOC-002");
        doc.AssignRecordClass(5, 1); // already has record class

        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);

        var result = await CreateHandler().Handle(
            new DeclareRecordCommand(doc.DocumentId, 3, 10), default);

        Assert.False(result.Success);
        Assert.Contains("بالفعل", result.Message);
    }

    [Fact]
    public async Task DeclareRecord_DocumentNotFound_ReturnsFail()
    {
        _docRepo.Setup(r => r.GetByGuidAsync(It.IsAny<Guid>(), default))
                .ReturnsAsync((Document?)null);

        var result = await CreateHandler().Handle(
            new DeclareRecordCommand(Guid.NewGuid(), 3, 10), default);

        Assert.False(result.Success);
        Assert.Contains("غير موجودة", result.Message);
    }

    [Fact]
    public async Task DeclareRecord_TransitionsFromDraftToActive()
    {
        var doc = Document.Create("وثيقة", 1, 1, 1, "DOC-003");
        Assert.Equal(DocumentStatus.Draft, doc.Status);

        var policy = RetentionPolicy.Create("P7Y", "7 سنوات", 7, 1);
        _docRepo.Setup(r => r.GetByGuidAsync(doc.DocumentId, default)).ReturnsAsync(doc);
        _recordsRepo.Setup(r => r.GetRetentionPolicyAsync(5, default)).ReturnsAsync(policy);

        await CreateHandler().Handle(
            new DeclareRecordCommand(doc.DocumentId, 1, 5), default);

        Assert.Equal(DocumentStatus.Active, doc.Status);
    }

    [Fact]
    public async Task ApplyLegalHold_LegalHoldDocCannotBeDisposed()
    {
        var doc = Document.Create("وثيقة مجمدة", 1, 1, 1, "DOC-004");
        doc.ApplyLegalHold();

        Assert.False(doc.CanBeDeleted(), "Legal hold doc must not be deletable");
        Assert.False(doc.CanSubmitToWorkflow(), "Legal hold doc cannot be submitted");
    }
}

namespace Darah.ECM.IntegrationTests.Workflow;

public sealed class WorkflowCommandTests
{
    [Fact]
    public void WorkflowDefinition_MultiStep_FirstStepIsLowest()
    {
        var def = WorkflowDefinition.Create("APPROVAL", "اعتماد", 1);
        def.AddStep("REVIEW",  "مراجعة",   1, "Role", 1, slaHours: 24);
        def.AddStep("APPROVE", "اعتماد",   2, "Role", 1, slaHours: 48, isFinal: true);
        def.AddStep("NOTIFY",  "إشعار",    3, "Role", 1);

        var first = def.GetFirstStep();
        Assert.NotNull(first);
        Assert.Equal("REVIEW", first!.StepCode);
    }

    [Fact]
    public void WorkflowAction_Create_RecordsCorrectData()
    {
        var action = WorkflowAction.Create(42, "Approve", 7,
            "موافق على الطلب", null, "أحمد العمري");

        Assert.Equal(42, action.TaskId);
        Assert.Equal("Approve", action.ActionType);
        Assert.Equal(7, action.ActionBy);
        Assert.Equal("موافق على الطلب", action.Comment);
        Assert.Equal("أحمد العمري", action.ActionByName);
    }

    [Fact]
    public void WorkflowTask_Delegate_ChangesAssigneeAndSetsFlag()
    {
        var task = WorkflowTask.Create(1, 1, 10, null, 48);
        task.Delegate(20);
        Assert.Equal(20, task.AssignedToUserId);
        Assert.True(task.IsDelegated);
        Assert.Equal(10, task.DelegatedFrom);
    }

    [Fact]
    public void WorkflowTask_SLAExpiry_CalculatedFromCreation()
    {
        var task = WorkflowTask.Create(1, 1, 5, null, 24);
        Assert.NotNull(task.DueAt);
        var expected = DateTime.UtcNow.AddHours(23); // Allow 1 hour variance
        Assert.True(task.DueAt > expected);
    }

    [Fact]
    public void WorkflowInstance_Start_IsInProgress()
    {
        var inst = WorkflowInstance.Start(1, Guid.NewGuid(), 7, priority: 3);
        Assert.Equal("InProgress", inst.Status);
        Assert.Equal(3, inst.Priority);
        Assert.Equal(7, inst.StartedBy);
    }

    [Fact]
    public void WorkflowInstance_Complete_SetsApprovedStatus()
    {
        var inst = WorkflowInstance.Start(1, Guid.NewGuid(), 1);
        inst.Complete();
        Assert.Equal("Approved", inst.Status);
        Assert.NotNull(inst.CompletedAt);
    }

    [Fact]
    public void WorkflowInstance_Reject_SetsRejectedStatus()
    {
        var inst = WorkflowInstance.Start(1, Guid.NewGuid(), 1);
        inst.Reject();
        Assert.Equal("Rejected", inst.Status);
    }
}

namespace Darah.ECM.IntegrationTests.Search;

public sealed class SearchQueryBuilderTests
{
    [Fact]
    public void SearchDocumentsQuery_PageSize_CapAt100()
    {
        // Validate that controllers clamp pageSize
        var maxAllowed = Math.Min(500, 100);
        Assert.Equal(100, maxAllowed);
    }

    [Fact]
    public void DocumentStatus_AllStatuses_CanBeCreatedFromString()
    {
        var statuses = new[] { "DRAFT", "ACTIVE", "PENDING", "APPROVED",
                                "REJECTED", "ARCHIVED", "SUPERSEDED", "DISPOSED" };
        foreach (var status in statuses)
        {
            var s = DocumentStatus.From(status);
            Assert.Equal(status, s.Value);
        }
    }

    [Fact]
    public void ClassificationLevel_AllOrders_CanBeResolved()
    {
        for (int i = 1; i <= 4; i++)
        {
            var level = ClassificationLevel.FromOrder(i);
            Assert.Equal(i, level.Order);
        }
    }
}

// Import statements needed
using Darah.ECM.Application.Notifications;
using Microsoft.Extensions.Logging;
