using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class CorrectiveActionCancellationWorkTests
{
    [Fact]
    public async Task ShouldReplaceActionWorkWithIndependentClosureWorkGivenCancellationProjected()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(4));
        var action = Assert.Single(finding.CorrectiveActions);
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<FindingReader>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var work = new FitzCorrectiveActionWorkItemDirectory(new InMemoryKvClient(), reader, TimeProvider.System);
        var closures = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(), reader, TimeProvider.System);
        var pattern = work.SourcePattern(fixture.TenantId);
        await ProjectAsync(work, FitzCorrectiveActionWorkItemDirectory.ProjectorName, pattern,
            ProjectionCheckpoint.Start, events, work.ApplyAsync);
        await ProjectAsync(closures, FitzFindingClosureWorkItemDirectory.ProjectorName, pattern,
            ProjectionCheckpoint.Start, events, closures.ApplyAsync);
        Assert.Single((await work.LoadProjectedProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        Assert.Empty((await closures.LoadProjectedProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        var workCheckpoint = await work.LoadCheckpointAsync(fixture.TenantId);
        var closureCheckpoint = await closures.LoadCheckpointAsync(fixture.TenantId);

        // Act
        var cancelled = await fixture.AsAsync(fixture.LeadUserId, new CancelCorrectiveAction(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, finding.FindingId, action.Revision, action.ActionId,
            "Duplicate; retained action removes access."));
        await ProjectAsync(work, FitzCorrectiveActionWorkItemDirectory.ProjectorName, pattern, workCheckpoint, events,
            work.ApplyAsync);
        await ProjectAsync(closures, FitzFindingClosureWorkItemDirectory.ProjectorName, pattern, closureCheckpoint, events,
            closures.ApplyAsync);
        var actionWork = await work.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);
        var closureWork = await closures.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.True(actionWork.IsSuccess, actionWork.Error?.Message);
        Assert.Empty(actionWork.Value);
        Assert.True(closureWork.IsSuccess, closureWork.Error?.Message);
        var candidate = Assert.Single(closureWork.Value);
        Assert.Equal(FindingClosureWork.Kind, candidate.Kind);
        Assert.Contains(fixture.OwnerMemberId, candidate.Excluded);
        Assert.Contains(fixture.LeadMemberId, candidate.Excluded);
        Assert.DoesNotContain(fixture.ApproverMemberId, candidate.Excluded);
        var resolved = Assert.Single(cancelled.CorrectiveActions);
        Assert.Null(resolved.CompletedByMemberId);
        Assert.Equal(fixture.LeadMemberId, resolved.CancelledByMemberId);
    }

    [Fact]
    public async Task ShouldKeepClosureUnavailableGivenCancellationLeavesAnotherActionOpen()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(4));
        var first = Assert.Single(finding.CorrectiveActions);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(4), "Separate work.");

        // Act
        var cancelled = await fixture.AsAsync(fixture.LeadUserId, new CancelCorrectiveAction(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, finding.FindingId, first.Revision, first.ActionId, "Duplicate work."));

        // Assert
        Assert.Null(FindingClosureWork.Candidate(cancelled));
        Assert.Single(cancelled.CorrectiveActions, action => action.Status == "open");
        Assert.Single(cancelled.CorrectiveActions, action => action.Status == "cancelled");
    }

    static async Task ProjectAsync(IProjectionStore store, string name, EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, IDomainEventReader events, Func<DomainEvent, CancellationToken, ValueTask> apply)
    {
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(name, pattern),
            checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await apply(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
