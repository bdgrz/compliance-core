using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportProjectionTests
{
    const string Directory = "ApplicationDirectoryV2";
    const string Imports = "ApplicationImportDirectoryV1";

    [Fact]
    public async Task ShouldProjectWholeBatchOnlyGivenCommittedInitialObservations()
    {
        // Arrange: the groups are applied (provisional) but the batch has not committed.
        await using var host = new ApplicationImportHost();
        var a = Uuid.CreateVersion4();
        var b = Uuid.CreateVersion4();
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(a, "record-a", "Alpha", "Alpha purpose"),
            ApplicationImportHost.Create(b, "record-b", "Beta", "Beta purpose")]);
        var record = await host.StartProcessAsync(batch.Id);
        foreach (var (stage, receiver, target) in new[]
                 {
                     ("claim", nameof(ClaimApplicationImportSource), record.Targets[0]),
                     ("reserve", nameof(ReserveApplicationImportTarget), record.Targets[1]),
                     ("reserve", nameof(ReserveApplicationImportTarget), record.Targets[2]),
                     ("apply", nameof(ApplyApplicationImportEffect), record.Targets[1]),
                     ("apply", nameof(ApplyApplicationImportEffect), record.Targets[2]),
                 })
            Assert.True((await host.SendAsync((await host.StepAsync(record, stage, receiver, target))!)).IsSuccess);
        await host.CatchUpAsync(Directory);
        var reader = host.Services.GetRequiredService<IApplicationDirectoryReader>();

        // Act
        var beforeA = await reader.GetAsync(host.TenantId, a);
        var beforeB = await reader.GetAsync(host.TenantId, b);
        var run = await host.RunProcessAsync(batch.Id);
        await host.CatchUpAsync(Directory);
        var viewA = await reader.GetAsync(host.TenantId, a);
        var viewB = await reader.GetAsync(host.TenantId, b);
        var history = await reader.ListRevisionsAsync(host.TenantId, a, 10, null);

        // Assert: nothing is visible before the commit; afterwards every target is, at its candidate revision.
        Assert.Null(beforeA);
        Assert.Null(beforeB);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(1, viewA!.Revision);
        Assert.Equal("Alpha", viewA.Name);
        Assert.Equal("Alpha purpose", viewA.Purpose);
        Assert.Equal("import", viewA.SourceKind);
        Assert.Equal("record-a", viewA.SourceIdentifier);
        Assert.Equal(host.ActorMemberId, viewA.LastChangedByMemberId);
        Assert.Equal("Beta", viewB!.Name);
        Assert.Single(history!.Items);
    }

    [Fact]
    public async Task ShouldProjectRetirementOnlyAfterCommitGivenCompleteCurrentImpact()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var target = await host.SeedApplicationAsync(2);
        var batch = await host.StageBatchAsync("declared_complete");
        await host.SealPlanAsync(batch.Id, [host.Retire(batch.Id, target, "record-b", 2)]);
        await host.CatchUpAsync(Directory);
        var reader = host.Services.GetRequiredService<IApplicationDirectoryReader>();
        var before = await reader.GetAsync(host.TenantId, target);

        // Act
        Assert.Equal(ProcessState.Completed, (await host.RunProcessAsync(batch.Id)).State);
        await host.CatchUpAsync(Directory);
        var after = await reader.GetAsync(host.TenantId, target);

        // Assert
        Assert.Equal("active", before!.Lifecycle);
        Assert.Equal("retired", after!.Lifecycle);
        Assert.Equal(3, after.Revision);
        Assert.Equal("Omitted by the complete source", after.Retirement!.Reason);
    }

    [Fact]
    public async Task ShouldProjectLifecycleStatesInOrderGivenBatchFacts()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var rowId = (await host.RowsAsync(batch)).Single().RowId;
        var directory = host.Services.GetRequiredService<IApplicationImportDirectoryReader>();
        var states = new List<string>();
        async Task Observe()
        {
            await host.CatchUpAsync(Imports);
            var view = await directory.GetAsync(host.TenantId, batch.Id);
            states.Add($"{view!.State}:{view.Revision}:{view.PendingCount}:{view.AppliedCount}");
        }

        // Act
        await Observe();
        Assert.True((await host.CorrelateAsync(batch.Id, rowId, 1, "create_new")).IsSuccess);
        await Observe();
        Assert.True((await host.AcceptAsync(batch.Id, 2)).IsSuccess);
        await Observe();
        await host.RunProcessAsync(batch.Id);
        await Observe();

        // Assert: staged, correlated, sealed (pending), then execution authorization and publication.
        Assert.Equal(["preview_ready:1:0:0", "preview_ready:2:0:0", "accepting:3:1:0", "committed:5:0:1"], states);
    }

    [Fact]
    public async Task ShouldProjectCancellationInTwoStepsGivenAcceptedThenCompleted()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var directory = host.Services.GetRequiredService<IApplicationImportDirectoryReader>();

        // Act
        Assert.True((await host.CancelAsync(batch.Id, 1)).IsSuccess);
        await host.CatchUpAsync(Imports);
        var canceling = await directory.GetAsync(host.TenantId, batch.Id);
        await host.ReactCancellationAsync(batch.Id);
        await host.CatchUpAsync(Imports);
        var canceled = await directory.GetAsync(host.TenantId, batch.Id);

        // Assert
        Assert.Equal(("canceling", 2L), (canceling!.State, canceling.Revision));
        Assert.Equal(("canceled", 3L), (canceled!.State, canceled.Revision));
        Assert.Equal(0, canceled.PendingCount);
    }
}
