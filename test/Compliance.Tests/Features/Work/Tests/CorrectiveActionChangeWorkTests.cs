using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class CorrectiveActionChangeWorkTests
{
    [Fact]
    public async Task ShouldRefreshWorkOwnerDescriptionAndDueDateGivenActionChanged()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        await using var scope = provider.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var directory = new FitzCorrectiveActionWorkItemDirectory(new InMemoryKvClient(),
            scope.ServiceProvider.GetRequiredService<FindingReader>(), TimeProvider.System);
        var pattern = directory.SourcePattern(fixture.TenantId);
        await ProjectAsync(directory, events, pattern, ProjectionCheckpoint.Start);
        var before = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        var checkpoint = await directory.LoadCheckpointAsync(fixture.TenantId);
        var command = CorrectiveActionChangeWorkflowTests.Change(fixture, finding);

        // Act
        await fixture.AsAsync(fixture.LeadUserId, command);
        await ProjectAsync(directory, events, pattern, checkpoint);
        var result = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var current = Assert.Single(result.Value);
        Assert.Equal(before.WorkItemId, current.WorkItemId);
        Assert.Equal(command.Description.Trim(), current.Summary);
        Assert.Equal(command.DueOn, current.DueOn);
        Assert.Equal(command.OwnerMemberId, current.Responsible.Id);
        Assert.NotEqual(before.Responsible.Id, current.Responsible.Id);
    }

    static async Task ProjectAsync(FitzCorrectiveActionWorkItemDirectory directory, IDomainEventReader events,
        EventStreamPattern pattern, ProjectionCheckpoint checkpoint)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            FitzCorrectiveActionWorkItemDirectory.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
