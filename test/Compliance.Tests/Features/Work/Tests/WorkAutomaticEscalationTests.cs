using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Events;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WorkAutomaticEscalationTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    public void ShouldRecordOnlyDueEscalationGivenSchedulerThreshold(int daysOverdue, bool expected)
    {
        // Arrange
        var owner = NewOwner();
        var today = new DateOnly(2026, 11, 4);
        var at = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = owner.EscalateAutomatically(WorkItemAssignment.AutomaticEscalationId(owner.ProgramId, owner.WorkItemId),
            0, Uuid.CreateVersion4(), "corrective_action", today.AddDays(-daysOverdue), today, at);

        // Assert
        Assert.Equal(expected, result.IsSuccess && result.Value);
        Assert.Equal(expected, owner.Escalated);
        Assert.Equal(expected ? 1 : 0, owner.Revision);
        Assert.Null(owner.AssigneeMemberId);
        Assert.Equal(expected ? 1 : 0, new AggregateScenario<WorkItemAssignment>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldKeepOriginalFactGivenRepeatedAutomaticEscalation()
    {
        // Arrange
        var owner = NewOwner();
        var today = new DateOnly(2026, 11, 4);
        var at = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var id = WorkItemAssignment.AutomaticEscalationId(owner.ProgramId, owner.WorkItemId);
        Assert.True(owner.EscalateAutomatically(id, 0, Uuid.CreateVersion4(), "corrective_action", today.AddDays(-7), today, at).Value);
        var original = Assert.IsType<WorkItemAutomaticallyEscalated>(Assert.Single(new AggregateScenario<WorkItemAssignment>(owner).PendingEvents));

        // Act
        var retry = owner.EscalateAutomatically(id, 0, original.SourceId, original.SourceKind, original.DueOn, today.AddDays(1), at.AddDays(1));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(retry.Value);
        Assert.Same(original, Assert.Single(new AggregateScenario<WorkItemAssignment>(owner).PendingEvents));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenChangedOwningRevisionOrWrongCommandIdentity()
    {
        // Arrange
        var owner = NewOwner();
        var today = new DateOnly(2026, 11, 4);
        var at = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        // Act
        var stale = owner.EscalateAutomatically(WorkItemAssignment.AutomaticEscalationId(owner.ProgramId, owner.WorkItemId),
            1, Uuid.CreateVersion4(), "corrective_action", today.AddDays(-7), today, at);
        var foreign = owner.EscalateAutomatically(Uuid.CreateVersion4(), 0, Uuid.CreateVersion4(),
            "corrective_action", today.AddDays(-7), today, at);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.False(foreign.IsSuccess);
        Assert.Empty(new AggregateScenario<WorkItemAssignment>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public async Task ShouldPreserveRecordedAssigneeAndOneFactGivenRepeatedSchedulerPass()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today.AddDays(-7));
        var item = Assert.Single((await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items);
        Assert.False(item.Escalated);
        await fixture.AsAsync(fixture.LeadUserId, new AssignWorkItem(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId));

        // Act
        await RunAsync(fixture);
        await RunAsync(fixture);
        var detail = await fixture.AsAsync(fixture.LeadUserId,
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId));

        // Assert
        Assert.True(detail.Item.Escalated);
        Assert.Equal("system", detail.Item.EscalatedBy);
        Assert.Equal(fixture.BackupMemberId, detail.Item.AssigneeMemberId);
        Assert.Equal(2, detail.Item.AssignmentRevision);
        Assert.Single(detail.History, value => value.Action == WorkItemAssignment.Escalate);
        Assert.Equal("system_process", detail.History[^1].By.Kind);
    }

    internal static async Task RunAsync(OperationsFixture fixture)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var sweep = new WorkEscalationSweep(services.GetRequiredService<ITenantDirectoryReader>(),
            services.GetRequiredService<ITenantActivity>(), services.GetRequiredService<IProgramDirectoryReader>(),
            services.GetRequiredService<WorkQueueReader>(), services.GetRequiredService<IAggregateExecutor>(),
            services.GetRequiredService<TimeProvider>());
        await sweep.ProcessProgramAsync(fixture.TenantId, fixture.ProgramId, CancellationToken.None);
    }

    static WorkItemAssignment NewOwner() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
}
