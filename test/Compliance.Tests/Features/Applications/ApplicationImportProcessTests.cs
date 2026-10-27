using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportProcessTests
{
    [Fact]
    public async Task ShouldPublishEveryTargetTogetherGivenImportGroupPartiallyApplied()
    {
        // Arrange: create A, link A and retire B (initial revisions A=0, B=5), sealed as one complete plan.
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync("declared_complete");
        var a = Uuid.CreateVersion4();
        var b = await host.SeedApplicationAsync(5);
        var rows = new[]
        {
            ApplicationImportHost.Create(a, "record-a"), ApplicationImportHost.Link(a, "record-a-alias", 1),
            host.Retire(batch.Id, b, "record-b", 5),
        };

        // Act: step 1, the full ImportFrozenPlanIntent and its source claim are sealed.
        var sealedBatch = await host.SealPlanAsync(batch.Id, rows);
        var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(host.Content, host.TenantId,
            sealedBatch.Plan!.FrozenPlan);
        var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(host.Content,
            host.TenantId, plan.TargetGroups);
        var plannedRows = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(host.Content,
            host.TenantId, plan.Rows);

        // Assert
        Assert.Equal(3, sealedBatch.Plan.RowCount);
        Assert.Equal(2, sealedBatch.Plan.TargetGroupCount);
        Assert.Equal(sealedBatch.Plan.FrozenPlan.Sha256, sealedBatch.Plan.PlanSha256);
        Assert.Equal(rows.Select(row => row.RowId), plannedRows.Select(row => row.RowId));
        Assert.Equal(new[] { a, b }.OrderBy(id => ApplicationImportEffects.TargetKey(id), StreamKeyOrder.Instance),
            groups.Select(group => group.ApplicationId));
        Assert.Equal([0UL, 5UL], groups.OrderBy(group => group.ApplicationId == b ? 1 : 0)
            .Select(group => group.InitialApplicationRevision));
        Assert.Equal([1UL, 6UL], groups.OrderBy(group => group.ApplicationId == b ? 1 : 0)
            .Select(group => group.CandidateContentRevision));
        Assert.Equal(host.ActorMemberId, plan.ApproverMemberId);
        Assert.Equal(0, plan.SourceClaims.Count);
        Assert.Equal(0, plan.SourceRevision);
        Assert.Equal(batch.Id, plan.BatchId);

        // Act: step 2, claim the source, acquire canonical holds and apply group A in one native append.
        var record = await host.StartProcessAsync(batch.Id);
        var sourceKey = record.Targets[0];
        var claimed = await host.SendAsync((await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource),
            sourceKey))!);
        var holdOrder = new List<Uuid>();
        foreach (var target in record.Targets.Where(key => key.StartsWith("applications/", StringComparison.Ordinal)))
        {
            var reserved = await host.SendAsync((await host.StepAsync(record, "reserve",
                nameof(ReserveApplicationImportTarget), target))!);
            Assert.IsType<ReceiverOutcome.Applied>(reserved.Value);
            holdOrder.Add(Uuid.Parse(target["applications/".Length..], null));
        }

        var applyA = await host.SendAsync((await host.StepAsync(record, "apply", nameof(ApplyApplicationImportEffect),
            ApplicationImportEffects.TargetKey(a)))!);
        var appA = await host.ApplicationAsync(a);
        var appB = await host.ApplicationAsync(b);

        // Assert: baseline A=0 checked once; create is revision 1 and the link neither overwrites nor increments it.
        Assert.IsType<ReceiverOutcome.Applied>(claimed.Value);
        Assert.IsType<ReceiverOutcome.Applied>(applyA.Value);
        Assert.Equal(groups.Select(group => group.ApplicationId), holdOrder);
        var holdA = appA.FindImportHold(batch.Id)!;
        Assert.Equal([1L, 1L], holdA.Effects.Select(effect => effect.ResultingRevision));
        Assert.Equal(rows[..2].Select(row => row.RowId), holdA.Effects.Select(effect => effect.Row.RowId));
        Assert.Equal(2, holdA.Effects.Count(effect => effect.Metadata is not null));
        Assert.All(holdA.Effects, effect => Assert.Equal(sealedBatch.Plan.FrozenPlan, effect.FrozenPlan));

        // Act: step 3, B is paused; the composed read must expose no new candidate without the whole-batch commit.
        var composedA = await host.Reader.HydrateApplicationAsync(host.TenantId, a);
        var composedB = await host.Reader.HydrateApplicationAsync(host.TenantId, b);

        // Assert
        Assert.False(composedA.IsCreated);
        Assert.Equal(0, composedA.Revision);
        Assert.False(composedB.IsRetired);
        Assert.Equal(5, composedB.Revision);
        Assert.False(appA.IsCreated);
        Assert.Empty(appB.GetPendingImportEffects());
        Assert.Null((await host.BatchAsync(batch.Id)).Committed);

        // Act: step 4, the driver resumes (replaying what was already done), applies B once and commits the batch.
        var run = await host.RunProcessAsync(batch.Id);
        var committed = (await host.BatchAsync(batch.Id)).Committed!;
        var effects = await ApplicationImportContent.ReadAsync<IReadOnlyList<ApplicationImportEffectProof>>(host.Content,
            host.TenantId, committed.EffectsManifest);
        var afterCommit = await host.ApplicationAsync(a);
        var publishedA = await host.Reader.HydrateApplicationAsync(host.TenantId, a);
        var publishedB = await host.Reader.HydrateApplicationAsync(host.TenantId, b);

        // Assert: one commit publishes every row, and the late settlement does not increment content again.
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Single(await host.EventsAsync<ApplicationImportCommitted>(sealedBatch.Stream));
        Assert.Equal(3, effects.Count);
        Assert.Equal(rows.Select(row => row.RowId).Order(), effects.Select(effect => effect.RowId).Order());
        Assert.Equal(committed.FrozenRows, plan.Rows.Content);
        Assert.Equal(plan.SourceRevision, committed.SourceGeneration);
        Assert.True(publishedA.IsCreated);
        Assert.Equal(1, publishedA.Revision);
        Assert.True(publishedB.IsRetired);
        Assert.Equal(6, publishedB.Revision);

        // Act: step 5, settle A and B late.
        var settledA = await host.EventsAsync<ApplicationImportTargetSettled>(afterCommit.Stream);
        var plainA = await host.ApplicationAsync(a);
        var plainB = await host.ApplicationAsync(b);

        // Assert: promotion keeps the candidate revision and replay of the target never consulted the live batch.
        var settlement = Assert.Single(settledA);
        Assert.Equal(ApplicationImportDisposition.Published, settlement.Disposition);
        Assert.Equal(1, settlement.Revision);
        Assert.True(plainA.IsCreated);
        Assert.Equal(1, plainA.Revision);
        Assert.True(plainB.IsRetired);
        Assert.Equal(6, plainB.Revision);
        var source = await host.SourceAsync();
        Assert.Null(source.ActiveBatchId);
        Assert.Equal([batch.Id], (await host.SourceHistoryAsync(source)).Published.Select(static fact => fact.BatchId));
    }

    [Fact]
    public async Task ShouldKeepTargetReplayLocalGivenPublishedBatchBeforeTargetSettlement()
    {
        // Arrange: the batch is published but its targets have not settled yet.
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var a = Uuid.CreateVersion4();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(a, "record-a")]);
        var record = await host.StartProcessAsync(batch.Id);
        foreach (var (stage, receiver, target) in new[]
                 {
                     ("claim", nameof(ClaimApplicationImportSource), record.Targets[0]),
                     ("reserve", nameof(ReserveApplicationImportTarget), ApplicationImportEffects.TargetKey(a)),
                     ("apply", nameof(ApplyApplicationImportEffect), ApplicationImportEffects.TargetKey(a)),
                 })
            Assert.True((await host.SendAsync((await host.StepAsync(record, stage, receiver, target))!)).IsSuccess);
        var commit = await host.SendAsync((await host.StepAsync(record, "publish",
            nameof(CommitApplicationImportBatch)))!);

        // Act
        var replayed = await host.ApplicationAsync(a);
        var composed = await host.PublishedApplicationAsync(a);
        var write = replayed.Revise(0, "Alpha", "Edited", null, host.ActorMemberId, "Import manager", host.Clock.GetUtcNow());

        // Assert: the target stream alone replays its own provisional facts; only the fixed-cut read composition
        // shows the published candidate, and a local write waits for the hold to settle.
        Assert.True(commit.IsSuccess, commit.Error?.Message);
        Assert.False(replayed.IsCreated);
        Assert.Equal(0, replayed.Revision);
        Assert.True(replayed.FindImportHold(batch.Id)!.IsActive);
        Assert.True(composed.IsCreated);
        Assert.Equal(1, composed.Revision);
        Assert.NotNull(write);
        Assert.NotNull(replayed.CheckPendingImportChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReserveInCanonicalKeyOrderGivenOppositeTargetInputOrder(bool reverseAuthoredOrder)
    {
        // Arrange: the same three applications, authored in canonical or in opposite row order.
        await using var host = new ApplicationImportHost();
        var ids = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var canonical = ids.OrderBy(id => ApplicationImportEffects.TargetKey(id), StreamKeyOrder.Instance).ToArray();
        var authored = reverseAuthoredOrder ? canonical.Reverse().ToArray() : canonical;
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, authored.Select((id, index) =>
            ApplicationImportHost.Create(id, $"record-{index}")).ToArray());

        // Act
        var started = await host.StartProcessAsync(batch.Id);
        var run = await host.RunProcessAsync(batch.Id);
        var order = await ReservationOrderAsync(host, batch.Id);

        // Assert: the hold order follows the canonical stream keys, never the authored order.
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(canonical.Select(ApplicationImportEffects.TargetKey), started.Targets.Skip(1));
        Assert.Equal(canonical, order);
    }

    [Fact]
    public async Task ShouldResumeWithIdenticalIdsAndPublishOnceGivenCrashAtEveryCheckpoint()
    {
        // Arrange: crash after k successful process record writes, for every k until the run completes unharmed.
        var plans = new List<string>();
        for (var crashPoint = 0; crashPoint < 80; crashPoint++)
        {
            var crash = new CrashOnceStore();
            await using var host = new ApplicationImportHost(crash.Wrap);
            var batch = await host.StageBatchAsync();
            var a = Uuid.CreateVersion4();
            var b = await host.SeedApplicationAsync(2);
            await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(a, "record-a"),
                ApplicationImportHost.Link(a, "record-a-alias", 1), ApplicationImportHost.Link(b, "record-b", 2)]);
            crash.ArmAfterReplaceCount(crashPoint);

            // Act
            var crashed = await Record.ExceptionAsync(() => host.RunProcessAsync(batch.Id));
            var resumed = await host.RunProcessAsync(batch.Id);

            // Assert: whatever step the worker crashed after, the resumed run produced each fact exactly once.
            Assert.Equal(ProcessState.Completed, resumed.State);
            var stream = host.NewBatch(batch.Id).Stream;
            Assert.Single(await host.EventsAsync<ApplicationImportCommitted>(stream));
            Assert.Single(await host.EventsAsync<ApplicationImportExecutionAuthorized>(stream));
            Assert.Single(await host.EventsAsync<ApplicationImportSourceAdmissionRecorded>(
                (await host.SourceAsync()).Stream));
            Assert.Single(await host.EventsAsync<ApplicationImportSourceSettled>((await host.SourceAsync()).Stream));
            var appA = await host.ApplicationAsync(a);
            var appB = await host.ApplicationAsync(b);
            Assert.Equal(2, (await host.EventsAsync<ApplicationImportEffectPending>(appA.Stream)).Count);
            Assert.Single(await host.EventsAsync<ApplicationImportEffectPending>(appB.Stream));
            Assert.Single(await host.EventsAsync<ApplicationImportTargetAuthorized>(appA.Stream));
            Assert.Single(await host.EventsAsync<ApplicationImportTargetSettled>(appA.Stream));
            Assert.Single(await host.EventsAsync<ApplicationImportTargetSettled>(appB.Stream));
            Assert.Equal(1, appA.Revision);
            Assert.Equal(2, appB.Revision);
            plans.Add(crashed is null ? "none" : crashed.GetType().Name);
            if (crashed is null)
                break;
        }

        Assert.Contains("none", plans);
        Assert.True(plans.Count > 10);
    }

    [Fact]
    public async Task ShouldKeepEveryTargetUnpublishedGivenCancelBeforeDelayedApply()
    {
        // Arrange: A is held and applied, B is reserved only; the apply of B is delayed.
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var a = Uuid.CreateVersion4();
        var b = Uuid.CreateVersion4();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(a, "record-a"),
            ApplicationImportHost.Create(b, "record-b")]);
        var record = await host.StartProcessAsync(batch.Id);
        var ordered = record.Targets.Skip(1).ToArray();
        Assert.True((await host.SendAsync((await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource),
            record.Targets[0]))!)).IsSuccess);
        foreach (var target in ordered)
            Assert.True((await host.SendAsync((await host.StepAsync(record, "reserve",
                nameof(ReserveApplicationImportTarget), target))!)).IsSuccess);
        Assert.True((await host.SendAsync((await host.StepAsync(record, "apply", nameof(ApplyApplicationImportEffect),
            ordered[0]))!)).IsSuccess);
        var delayed = (await host.StepAsync(record, "apply", nameof(ApplyApplicationImportEffect), ordered[1]))!;

        // Act: the cancellation lands, the reactor fences every target, completes and releases.
        var canceled = await host.CancelAsync(batch.Id, 3);
        await host.ReactCancellationAsync(batch.Id);
        var late = await host.SendAsync(delayed);
        var ended = await host.BatchAsync(batch.Id);
        var source = await host.SourceAsync();

        // Assert
        Assert.True(canceled.IsSuccess, canceled.Error?.Message);
        Assert.Equal("canceled", ended.State);
        Assert.IsType<ReceiverOutcome.Superseded>(late.Value);
        Assert.Null(ended.Committed);
        foreach (var target in ordered)
        {
            var application = await host.ApplicationAsync(Uuid.Parse(target["applications/".Length..], null));
            var hold = application.FindImportHold(batch.Id)!;
            Assert.NotNull(hold.Fence);
            Assert.NotNull(hold.Settled);
            Assert.Equal(ApplicationImportDisposition.Canceled, hold.Settled.Disposition);
            Assert.False(application.IsCreated);
            Assert.Equal(0, application.Revision);
        }

        Assert.Null(source.ActiveBatchId);
        Assert.Empty((await host.SourceHistoryAsync(source)).Published);
        var driven = await host.RunProcessAsync(batch.Id);
        Assert.True(driven.IsTerminal);
        Assert.Single(await host.EventsAsync<ApplicationImportCanceled>(ended.Stream));
        Assert.Single(await host.EventsAsync<ApplicationImportSourceSettled>(source.Stream));
    }

    [Fact]
    public async Task ShouldDiscardEveryAppliedEffectGivenCancelAfterEveryTargetAppliedAndBeforeCommit()
    {
        // Arrange: both groups are applied; the commit command is built from the complete proofs but delayed.
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var a = Uuid.CreateVersion4();
        var b = Uuid.CreateVersion4();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(a, "record-a"),
            ApplicationImportHost.Create(b, "record-b")]);
        var record = await host.StartProcessAsync(batch.Id);
        Assert.True((await host.SendAsync((await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource),
            record.Targets[0]))!)).IsSuccess);
        foreach (var target in record.Targets.Skip(1))
            Assert.True((await host.SendAsync((await host.StepAsync(record, "reserve",
                nameof(ReserveApplicationImportTarget), target))!)).IsSuccess);
        foreach (var target in record.Targets.Skip(1))
            Assert.True((await host.SendAsync((await host.StepAsync(record, "apply",
                nameof(ApplyApplicationImportEffect), target))!)).IsSuccess);
        var delayedCommit = (await host.StepAsync(record, "publish", nameof(CommitApplicationImportBatch)))!;

        // Act
        var canceled = await host.CancelAsync(batch.Id, 3);
        await host.ReactCancellationAsync(batch.Id);
        var commit = await host.SendAsync(delayedCommit);
        var ended = await host.BatchAsync(batch.Id);

        // Assert: the fence wins over the delayed commit, and no candidate ever becomes the published application.
        Assert.True(canceled.IsSuccess, canceled.Error?.Message);
        Assert.IsType<ReceiverOutcome.Superseded>(commit.Value);
        Assert.Equal("canceled", ended.State);
        Assert.Null(ended.Committed);
        foreach (var id in new[] { a, b })
        {
            var application = await host.ApplicationAsync(id);
            var composed = await host.Reader.HydrateApplicationAsync(host.TenantId, id);
            var hold = application.FindImportHold(batch.Id)!;
            Assert.Single(hold.Effects);
            Assert.Equal(ApplicationImportDisposition.Canceled, hold.Settled!.Disposition);
            Assert.False(application.IsCreated);
            Assert.False(composed.IsCreated);
            Assert.Equal(0, application.Revision);
            Assert.Equal(0, composed.Revision);
        }
    }

    [Fact]
    public async Task ShouldFenceTargetsBeforeReleaseGivenCancelAfterAppliedGroupAndFenceWinsOverLateReserve()
    {
        // Arrange: only the source is claimed; no target was reserved yet.
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var a = Uuid.CreateVersion4();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(a, "record-a")]);
        var record = await host.StartProcessAsync(batch.Id);
        Assert.True((await host.SendAsync((await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource),
            record.Targets[0]))!)).IsSuccess);
        var lateReserve = (await host.StepAsync(record, "reserve", nameof(ReserveApplicationImportTarget),
            ApplicationImportEffects.TargetKey(a)))!;

        // Act
        var canceled = await host.CancelAsync(batch.Id, 3);
        await host.ReactCancellationAsync(batch.Id);
        var reserved = await host.SendAsync(lateReserve);
        var application = await host.ApplicationAsync(a);

        // Assert: the fence was recorded although the hold was never acquired, and the delayed reserve meets it.
        Assert.True(canceled.IsSuccess);
        Assert.IsType<ReceiverOutcome.Superseded>(reserved.Value);
        var hold = application.FindImportHold(batch.Id)!;
        Assert.Null(hold.Reserved);
        Assert.NotNull(hold.Fence);
        Assert.NotNull(hold.Settled);
        Assert.Null(application.CheckPendingImportChanges());
        Assert.Equal("canceled", (await host.BatchAsync(batch.Id)).State);
    }

    [Fact]
    public async Task ShouldConflictGivenCancellationAfterPublication()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var a = Uuid.CreateVersion4();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(a, "record-a")]);
        Assert.Equal(ProcessState.Completed, (await host.RunProcessAsync(batch.Id)).State);
        var committed = await host.BatchAsync(batch.Id);

        // Act
        var canceled = await host.CancelAsync(batch.Id, committed.Revision);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, canceled.Error?.Kind);
        Assert.Equal("committed", (await host.BatchAsync(batch.Id)).State);
    }

    [Fact]
    public async Task ShouldCompleteCancellationWithoutProcessGivenUnsealedBatch()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();

        // Act
        var canceled = await host.CancelAsync(batch.Id, 1);
        await host.ReactCancellationAsync(batch.Id);
        var ended = await host.BatchAsync(batch.Id);

        // Assert
        Assert.True(canceled.IsSuccess, canceled.Error?.Message);
        Assert.Equal("canceled", ended.State);
        Assert.Equal("Superseded", ended.Canceled!.Reason);
        Assert.Equal(host.ActorMemberId, ended.Canceled.ActorMemberId);
        Assert.Null((await host.SourceAsync()).ActiveBatchId);
    }

    [Fact]
    public async Task ShouldReplayOriginalAcknowledgmentGivenExactAcceptAndCancelRetryAfterLaterChange()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var rowId = (await host.RowsAsync(batch)).Single().RowId;
        Assert.True((await host.CorrelateAsync(batch.Id, rowId, 1, "create_new")).IsSuccess);
        var accept = Uuid.CreateVersion4();
        var first = await host.AcceptAsync(batch.Id, 2, accept);
        var cancel = Uuid.CreateVersion4();
        var canceled = await host.CancelAsync(batch.Id, 3, cancel);
        await host.ReactCancellationAsync(batch.Id);

        // Act
        var acceptAgain = await host.AcceptAsync(batch.Id, 2, accept);
        var cancelAgain = await host.CancelAsync(batch.Id, 3, cancel);
        var changedAccept = await host.AcceptAsync(batch.Id, 3, accept);
        var changedCancel = await host.CancelAsync(batch.Id, 99, cancel);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(first.Value, acceptAgain.Value);
        Assert.Equal(canceled.Value, cancelAgain.Value);
        Assert.Equal(accept, first.Value!.OperationId);
        Assert.Equal(RequestErrorKind.Conflict, changedAccept.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedCancel.Error?.Kind);
    }

    [Fact]
    public async Task ShouldFreezeEveryRowGivenPlanLargerThanOneFrame()
    {
        // Arrange: 150 rows of 1,500-character purposes serialize to well over one 64 KiB frame.
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var purpose = new string('p', 1500);
        var rows = Enumerable.Range(0, 150).Select(index => ApplicationImportHost.Create(Uuid.CreateVersion4(),
            $"record-{index:D3}", $"Application {index:D3}", purpose)).ToArray();

        // Act
        var sealedBatch = await host.SealPlanAsync(batch.Id, rows);
        var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(host.Content, host.TenantId,
            sealedBatch.Plan!.FrozenPlan);
        var frozen = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(host.Content,
            host.TenantId, plan.Rows);
        var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(host.Content,
            host.TenantId, plan.TargetGroups);

        // Assert
        Assert.True(plan.Rows.Content.Bytes > 64 * 1024);
        Assert.Equal(150, plan.Rows.Count);
        Assert.Equal(rows.Select(row => row.RowId), frozen.Select(row => row.RowId));
        Assert.Equal(150, groups.Count);
        Assert.Equal(150, sealedBatch.Plan.RowCount);
        Assert.Equal(150, sealedBatch.Plan.TargetGroupCount);
    }

    [Fact]
    public async Task ShouldMatchCatalogGivenRegisteredProcessDefinition()
    {
        // Arrange
        await using var host = new ApplicationImportHost();

        // Act
        var definition = host.Definition;

        // Assert
        Assert.True(definition.Ordered);
        Assert.Equal(["ApplicationImportPlanSealed"], definition.RootSourceEvents);
        Assert.Equal(["claim", "reserve", "apply", "publish", "settle"], definition.Stages.Select(stage => stage.Stage));
        Assert.Equal([["ClaimApplicationImportSource"], ["ReserveApplicationImportTarget"],
            ["ApplyApplicationImportEffect"], ["CommitApplicationImportBatch"],
            ["ReleaseApplicationImportTarget", "ReleaseApplicationImportSource"]],
            definition.Stages.Select(stage => stage.Receivers));
        Assert.Equal([true, true, true, false, true], definition.Stages.Select(stage => stage.IsPerTarget));
        Assert.Equal(["CancelApplicationImport"], definition.Abort);
    }

    static async Task<List<Uuid>> ReservationOrderAsync(ApplicationImportHost host, Uuid batchId)
    {
        var order = new List<Uuid>();
        await foreach (var record in host.Events.ReadAsync(EventStreamPattern.ForPattern(host.TenantId.ToString())))
            if (record.Event is ApplicationImportTargetAuthorized authorized && authorized.BatchId == batchId)
                order.Add(authorized.ApplicationId);
        return order;
    }

    /// <summary>Decorates the store so one ReplaceAsync, chosen by count, fails after the receiver already ran.</summary>
    sealed class CrashOnceStore
    {
        int _remaining = int.MaxValue;

        public void ArmAfterReplaceCount(int successfulReplacesBeforeCrash) => _remaining = successfulReplacesBeforeCrash;

        public IProcessRecordStore Wrap(IProcessRecordStore inner) => new Decorator(inner, this);

        sealed class Decorator(IProcessRecordStore inner, CrashOnceStore owner) : IProcessRecordStore
        {
            public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) => inner.ReadAsync(key, ct);

            public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
                inner.CreateIfAbsentAsync(record, ct);

            public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default)
            {
                if (owner._remaining-- == 0)
                    throw new InvalidOperationException("Worker crashed before the CAS.");
                return inner.ReplaceAsync(record, expectedVersion, ct);
            }

            public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
                CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
        }
    }
}
