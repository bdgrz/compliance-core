using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class DeclaredApplicationImportHoldTests
{
    [Fact]
    public async Task ShouldBlockEveryLocalWriteGivenUnsettledImportHoldAndReleaseThemAfterSettlement()
    {
        // Arrange: a published batch whose target hold has not settled yet.
        await using var host = new ApplicationImportHost();
        var target = await host.SeedApplicationAsync();
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Link(target, "record-1", 1)]);
        var record = await host.StartProcessAsync(batch.Id);
        await StepAsync(host, record, "claim", nameof(ClaimApplicationImportSource), record.Targets[0]);
        await StepAsync(host, record, "reserve", nameof(ReserveApplicationImportTarget), record.Targets[1]);

        // Act
        var held = await host.ApplicationAsync(target);
        var revise = held.Revise(1, "Seeded", "Blocked edit", null, host.ActorMemberId, "Import manager",
            host.Clock.GetUtcNow());
        var declare = held.Declare("Seeded", "Existing purpose", null, host.ActorMemberId, "Import manager",
            host.Clock.GetUtcNow());
        var retire = held.Retire(1, host.Clock.GetUtcNow(), "Retire", null, host.ActorMemberId, "Import manager",
            host.Clock.GetUtcNow());
        var run = await host.RunProcessAsync(batch.Id);
        var released = await host.ApplicationAsync(target);
        var write = released.Revise(1, "Seeded", "Allowed edit", null, host.ActorMemberId, "Import manager",
            host.Clock.GetUtcNow());

        // Assert
        Assert.NotNull(held.CheckPendingImportChanges());
        Assert.True(held.CheckPendingImportChanges()!.IsTransient);
        Assert.NotNull(revise);
        Assert.NotNull(retire);
        Assert.Equal(RequestErrorKind.Conflict, declare.Error?.Kind);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Null(released.CheckPendingImportChanges());
        Assert.Null(write);
        Assert.Equal(2, released.Revision);
    }

    [Fact]
    public async Task ShouldReserveIdentityGivenCreateHoldAndRefuseLocalDeclaration()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var id = Uuid.CreateVersion4();
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(id, "record-1")]);
        var record = await host.StartProcessAsync(batch.Id);
        await StepAsync(host, record, "claim", nameof(ClaimApplicationImportSource), record.Targets[0]);
        await StepAsync(host, record, "reserve", nameof(ReserveApplicationImportTarget), record.Targets[1]);

        // Act
        var held = await host.ApplicationAsync(id);
        var declared = held.Declare("Local", "Local purpose", null, host.ActorMemberId, "Import manager",
            host.Clock.GetUtcNow());

        // Assert
        Assert.True(held.ReservesIdentity);
        Assert.Equal(RequestErrorKind.Conflict, declared.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRefuseReservationGivenAnotherBatchHoldOrStaleBaseline()
    {
        // Arrange: batch one holds the target; batch two froze the same baseline.
        await using var host = new ApplicationImportHost();
        var target = await host.SeedApplicationAsync();
        var first = await host.StageBatchAsync(key: "first");
        var second = await host.StageBatchAsync(key: "second");
        await host.SealPlanAsync(first.Id, [ApplicationImportHost.Link(target, "record-1", 1)]);
        await host.SealPlanAsync(second.Id, [ApplicationImportHost.Link(target, "record-2", 1)]);
        var one = await host.StartProcessAsync(first.Id);
        var two = await host.StartProcessAsync(second.Id);
        await StepAsync(host, one, "claim", nameof(ClaimApplicationImportSource), one.Targets[0]);
        await StepAsync(host, two, "claim", nameof(ClaimApplicationImportSource), two.Targets[0]);
        await StepAsync(host, one, "reserve", nameof(ReserveApplicationImportTarget), one.Targets[1]);

        // Act
        var contested = await host.SendAsync((await host.StepAsync(two, "reserve",
            nameof(ReserveApplicationImportTarget), two.Targets[1]))!);
        var application = await host.ApplicationAsync(target);

        // Assert: the second batch waits (transient) and holds nothing.
        Assert.Equal(RequestErrorKind.Conflict, contested.Error?.Kind);
        Assert.True(contested.Error!.IsTransient);
        Assert.NotNull(application.FindImportHold(first.Id)?.Reserved);
        Assert.Null(application.FindImportHold(second.Id));
        Assert.Equal(first.Id, Assert.Single(application.ImportHolds).BatchId);
    }

    [Fact]
    public async Task ShouldPersistOneNativeBatchGivenApplyRetry()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var id = Uuid.CreateVersion4();
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(id, "record-1"),
            ApplicationImportHost.Link(id, "record-2", 1)]);
        var record = await host.StartProcessAsync(batch.Id);
        await StepAsync(host, record, "claim", nameof(ClaimApplicationImportSource), record.Targets[0]);
        await StepAsync(host, record, "reserve", nameof(ReserveApplicationImportTarget), record.Targets[1]);
        var apply = (await host.StepAsync(record, "apply", nameof(ApplyApplicationImportEffect), record.Targets[1]))!;

        // Act
        var first = await host.SendAsync(apply);
        var retry = await host.SendAsync(apply);
        var application = await host.ApplicationAsync(id);

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(first.Value);
        var replayed = Assert.IsType<ReceiverOutcome.Replayed>(retry.Value);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact, replayed.Fact);
        Assert.Equal(2, application.GetPendingImportEffects().Count);
        Assert.Equal(2, (await host.EventsAsync<ApplicationImportEffectPending>(application.Stream)).Count);
        Assert.False(application.IsCreated);
    }

    [Fact]
    public async Task ShouldRejectCorruptReplayGivenImportFactsWithoutTheirHold()
    {
        // Arrange: a real reservation to copy facts from, replayed onto streams that never held the target.
        await using var host = new ApplicationImportHost();
        var id = Uuid.CreateVersion4();
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(id, "record-1")]);
        var record = await host.StartProcessAsync(batch.Id);
        await StepAsync(host, record, "claim", nameof(ClaimApplicationImportSource), record.Targets[0]);
        await StepAsync(host, record, "reserve", nameof(ReserveApplicationImportTarget), record.Targets[1]);
        var reserved = (await host.ApplicationAsync(id)).FindImportHold(batch.Id)!.Reserved!;
        var effect = new ApplicationImportEffectPending(host.TenantId, id, 1, new string('0', 64), reserved.Intent,
            ApplicationImportHost.Create(id, "record-x"), host.Clock.GetUtcNow(), reserved.Operation, reserved.Intent,
            null, null, batch.Id, 1);
        var settled = new ApplicationImportTargetSettled(reserved.Owner, 0, 1, reserved.Intent, null,
            reserved.Operation, null, null, batch.Id, id, ApplicationImportDisposition.Published);

        // Act
        var effectWithoutHold = Record.Exception(() => Replay(new DeclaredApplication(host.TenantId, id), effect));
        var settledWithoutHold = Record.Exception(() => Replay(new DeclaredApplication(host.TenantId, id), settled));
        var reservedTwice = Record.Exception(() => Replay(new DeclaredApplication(host.TenantId, id), reserved, reserved));
        var publishedUnapplied = Record.Exception(() => Replay(new DeclaredApplication(host.TenantId, id), reserved,
            settled));

        // Assert
        Assert.IsType<InvalidOperationException>(effectWithoutHold);
        Assert.IsType<InvalidOperationException>(settledWithoutHold);
        Assert.IsType<InvalidOperationException>(reservedTwice);
        Assert.IsType<InvalidOperationException>(publishedUnapplied);
    }

    [Fact]
    public async Task ShouldPromoteCandidateOnceGivenPublishedSettlementAndKeepRevisionOnReplay()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var created = Uuid.CreateVersion4();
        var retired = await host.SeedApplicationAsync(3);
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(created, "record-1"),
            host.Retire(batch.Id, retired, "record-2", 3)]);

        // Act
        var run = await host.RunProcessAsync(batch.Id);
        var again = await host.RunProcessAsync(batch.Id);
        var createdApp = await host.ApplicationAsync(created);
        var retiredApp = await host.ApplicationAsync(retired);

        // Assert
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(ProcessState.Completed, again.State);
        Assert.True(createdApp.IsCreated);
        Assert.Equal(1, createdApp.Revision);
        Assert.True(retiredApp.IsRetired);
        Assert.Equal(4, retiredApp.Revision);
        Assert.Single(await host.EventsAsync<ApplicationImportTargetSettled>(retiredApp.Stream));
    }

    static void Replay(DeclaredApplication application, params DomainEvent[] facts) =>
        new AggregateScenario<DeclaredApplication>(application).Given(facts);

    static async Task StepAsync(ApplicationImportHost host, ProcessRecord record, string stage, string receiver,
        string target)
    {
        var result = await host.SendAsync((await host.StepAsync(record, stage, receiver, target))!);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
}
