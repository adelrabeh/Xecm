using Darah.ECM.Domain.Entities;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.Entities;

public sealed class WorkflowTaskTests
{
    [Fact]
    public void Create_WithSLA_SetsDueAt()
    {
        var task = WorkflowTask.Create(1, 1, 42, null, 24);
        Assert.NotNull(task.DueAt);
        Assert.True(task.DueAt > DateTime.UtcNow);
        Assert.True(task.DueAt <= DateTime.UtcNow.AddHours(25)); // ~24 hours
    }

    [Fact]
    public void Create_WithoutSLA_DueAtIsNull()
        => Assert.Null(WorkflowTask.Create(1, 1, 42, null, null).DueAt);

    [Fact]
    public void Create_DefaultStatus_IsPending()
        => Assert.Equal("Pending", WorkflowTask.Create(1, 1, 1, null, null).Status);

    [Fact]
    public void Complete_SetsStatusAndTimestamp()
    {
        var task = WorkflowTask.Create(1, 1, 42, null, null);
        task.Complete(42);
        Assert.Equal("Completed", task.Status);
        Assert.Equal(42, task.CompletedBy);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public void MarkOverdue_SetsFlag()
    {
        var task = WorkflowTask.Create(1, 1, 1, null, null);
        task.MarkOverdue();
        Assert.True(task.IsOverdue);
    }

    [Fact]
    public void MarkEscalated_SetsTimestamp()
    {
        var task = WorkflowTask.Create(1, 1, 1, null, null);
        task.MarkEscalated();
        Assert.NotNull(task.EscalatedAt);
    }

    [Fact]
    public void Delegate_ChangesAssigneeAndSetsFlag()
    {
        var task = WorkflowTask.Create(1, 1, 10, null, null);
        task.Delegate(99);
        Assert.Equal(99, task.AssignedToUserId);
        Assert.True(task.IsDelegated);
        Assert.Equal(10, task.DelegatedFrom);
    }

    [Fact]
    public void MarkSLABreachNotified_SetsTimestamp()
    {
        var task = WorkflowTask.Create(1, 1, 1, null, null);
        task.MarkSLABreachNotified();
        Assert.NotNull(task.SLABreachNotifiedAt);
    }
}
