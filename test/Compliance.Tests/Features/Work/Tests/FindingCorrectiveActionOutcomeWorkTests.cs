using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class FindingCorrectiveActionOutcomeWorkTests
{
    [Fact]
    public async Task ShouldRefreshClosureWorkIdentityGivenFindingRecordsActionOutcome()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await FindingCorrectiveActionOutcomeWorkflowTests.TerminalAsync(fixture, "completed");
        await using var scope = provider.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(),
            scope.ServiceProvider.GetRequiredService<FindingReader>(), TimeProvider.System);
        var pattern = directory.SourcePattern(fixture.TenantId);
        await ProjectAsync(directory, events, pattern, ProjectionCheckpoint.Start);
        var previous = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        var checkpoint = await directory.LoadCheckpointAsync(fixture.TenantId);
        var report = FindingCorrectiveActionOutcomeWorkflowTests.Report(fixture, finding, source, "completed");

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(report).ExpectSuccess();
        await ProjectAsync(directory, events, pattern, checkpoint);
        var current = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);
        var refreshed = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        Assert.True(current.IsSuccess, current.Error?.Message);
        var candidate = Assert.Single(current.Value);
        Assert.Equal(FindingClosureWork.Candidate(refreshed)!.WorkItemId, candidate.WorkItemId);
        Assert.NotEqual(previous.WorkItemId, candidate.WorkItemId);
        Assert.Equal(finding.Revision + 1, refreshed.Revision);
    }

    static async Task ProjectAsync(FitzFindingClosureWorkItemDirectory directory, IDomainEventReader events,
        EventStreamPattern pattern, ProjectionCheckpoint checkpoint)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            FitzFindingClosureWorkItemDirectory.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
