using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class TypeIIRangeProcessTests
{
    [Fact]
    public async Task ShouldHoldOldRangeGivenTypeIIAmendmentCanceledBeforeDelayedApply()
    {
        // Arrange: active inclusive range R1 with grant G1; the amendment accepts a complete R2 intent.
        await using var fixture = new TypeIIRangeFixture();
        var period = await fixture.StartPeriodAsync();
        var firstGeneration = (await fixture.CalendarAsync()).Find(period)!.Generation;
        var request = fixture.Amendment(period, 1, null, fixture.FirstEnd.AddDays(7));
        var admitted = await fixture.AmendAsync(request);
        var record = await fixture.Host.StartProcessAsync(fixture.TenantId, fixture.ProgramId, period,
            request.CommandId);

        // Act: step 1, the old and new range are both held while the amendment is pending.
        var acquired = await fixture.Host.SendAsync(await fixture.Host.StepAsync(record, "acquire",
            nameof(AcquireTypeIIPeriodRange)));
        var held = await fixture.CalendarAsync();
        var pending = await fixture.PeriodAsync(period);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var acquiredFact = Assert.IsType<ReceiverOutcome.Applied>(acquired.Value).Fact;
        Assert.Equal(TypeIIRangeReservationState.Active, held.Find(period)!.State);
        Assert.Equal(TypeIIRangeReservationState.Pending, held.Find(request.CommandId)!.State);
        Assert.Equal(firstGeneration + 1, held.Find(request.CommandId)!.Generation);
        Assert.NotNull(held.FindOverlap(Uuid.CreateVersion4(), new ObservationRange(fixture.FirstStart, fixture.FirstStart)));
        Assert.NotNull(held.FindOverlap(Uuid.CreateVersion4(), new ObservationRange(fixture.FirstEnd.AddDays(3),
            fixture.FirstEnd.AddDays(5))));
        Assert.Equal(request.CommandId, pending.PendingOperationId);
        Assert.Equal(1, pending.Revision);

        // Act: step 2, the cancellation lands before the delayed apply and fences it before any calendar release.
        var canceled = await fixture.CancelAsync(fixture.Cancellation(period, request.CommandId, 1));
        var fenced = await fixture.PeriodAsync(period);
        var beforeRelease = await fixture.CalendarAsync();

        // Assert
        Assert.True(canceled.IsSuccess, canceled.Error?.Message);
        Assert.IsType<TypeIIPeriodAmendmentCanceled>(fenced.TerminalFor(request.CommandId));
        Assert.Null(fenced.PendingOperationId);
        Assert.Equal(TypeIIRangeReservationState.Pending, beforeRelease.Find(request.CommandId)!.State);
        Assert.Equal(held.Revision, beforeRelease.Revision);

        // Act: step 3, the old apply is delivered after the cancellation.
        var withPermit = record with
        {
            Facts = new Dictionary<string, NativeFactReference> { [nameof(AcquireTypeIIPeriodRange)] = acquiredFact },
        };
        var lateApply = await fixture.Host.SendAsync(await fixture.Host.StepAsync(withPermit, "apply",
            nameof(ApplyTypeIIPeriodRange)));
        await fixture.Host.ReleaseAsync<TypeIIPeriodAmendmentCanceled>(fixture.StreamOf(period));
        var released = await fixture.CalendarAsync();

        // Assert: no ObservationPeriodAmended; only the pending new grant is released and old R1 stays active.
        Assert.IsType<ReceiverOutcome.Superseded>(lateApply.Value);
        Assert.Empty(await fixture.Host.EventsAsync<ObservationPeriodAmended>(fixture.StreamOf(period)));
        Assert.Equal(TypeIIRangeReservationState.Released, released.Find(request.CommandId)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, released.Find(period)!.State);
        Assert.NotNull(released.FindOverlap(Uuid.CreateVersion4(), new ObservationRange(fixture.FirstStart,
            fixture.FirstStart)));
        Assert.Null(released.FindOverlap(Uuid.CreateVersion4(), new ObservationRange(fixture.FirstEnd.AddDays(3),
            fixture.FirstEnd.AddDays(5))));

        // Act: step 4, another amendment applies, then the old cancellation and a stale close are delivered.
        var next = fixture.Amendment(period, 1, null, fixture.FirstEnd.AddDays(14));
        Assert.True((await fixture.AmendAsync(next)).IsSuccess);
        var run = await fixture.RunAsync(period, next.CommandId);
        await fixture.Host.ReleaseAsync<TypeIIPeriodAmendmentCanceled>(fixture.StreamOf(period));
        var afterOldCancellation = await fixture.CalendarAsync();
        await fixture.FreezeCloseAsync(period);
        var closeFact = Assert.Single(await fixture.Host.EventsAsync<ObservationPeriodCloseFrozen>(
            fixture.StreamOf(period)));
        var closed = await fixture.PeriodAsync(period);
        var firstIntent = TypeIIPeriod.IntentOf(closed.AcceptedFor(period))!;
        var oldClose = await fixture.Host.SendAsync(new SettleTypeIIPeriodRange(Uuid.CreateVersion4(),
            NativeFactReference.Of(closed.Stream, closed.FactById(closeFact.Metadata.EventId)!), fixture.CalendarStream,
            period, 1, null, firstIntent, period));
        var afterOldClose = await fixture.CalendarAsync();
        await fixture.Host.ReleaseAsync<ObservationPeriodCloseFrozen>(fixture.StreamOf(period));
        var afterClose = await fixture.CalendarAsync();

        // Assert: only the exact CalendarAuthorizationId/CalendarGeneration releases; no newer grant is lost.
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(TypeIIRangeReservationState.Active, afterOldCancellation.Find(next.CommandId)!.State);
        Assert.Equal(TypeIIRangeReservationState.Released, afterOldCancellation.Find(period)!.State);
        Assert.Equal(next.CommandId, closeFact.CalendarAuthorizationId);
        Assert.Equal(afterOldCancellation.Find(next.CommandId)!.Generation, closeFact.CalendarGeneration);
        Assert.Equal(RequestErrorKind.Conflict, oldClose.Error?.Kind);
        Assert.False(oldClose.Error?.IsTransient);
        Assert.Equal(TypeIIRangeReservationState.Active, afterOldClose.Find(next.CommandId)!.State);
        Assert.Equal(afterOldCancellation.Revision, afterOldClose.Revision);
        Assert.Equal(TypeIIRangeReservationState.Released, afterClose.Find(next.CommandId)!.State);
    }

    [Fact]
    public async Task ShouldPersistOpeningIntentBeforeCalendarAndRecoverGivenCrashAfterAcquisition()
    {
        // Arrange
        var crash = new CrashOnceStore();
        await using var fixture = new TypeIIRangeFixture(crash.Wrap);
        var period = await fixture.AcceptOpeningAsync();
        var calendarBefore = await fixture.CalendarAsync();
        var accepted = await fixture.PeriodAsync(period);
        crash.ArmAfterReplaceCount(1);

        // Act: the worker crashes after the acquisition committed and before its checkpoint.
        var crashed = await Record.ExceptionAsync(() => fixture.RunAsync(period, period));
        var midway = await fixture.CalendarAsync();
        var resumed = await fixture.RunAsync(period, period);
        var calendar = await fixture.CalendarAsync();
        var started = await fixture.PeriodAsync(period);

        // Assert
        Assert.Equal(0, calendarBefore.Revision);
        Assert.NotNull(accepted.Pending);
        Assert.IsType<InvalidOperationException>(crashed);
        Assert.Equal(1, midway.Revision);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Single(await fixture.Host.EventsAsync<TypeIIPeriodRangeAuthorized>(fixture.CalendarStream));
        Assert.Equal(TypeIIRangeReservationState.Active, calendar.Find(period)!.State);
        Assert.Equal(1, started.Revision);
        Assert.Single(await fixture.Host.EventsAsync<ObservationPeriodStarted>(fixture.StreamOf(period)));
    }

    [Fact]
    public async Task ShouldGrantOneRangeAndConflictTheOtherGivenCompetingOverlappingStartsDeliveredConcurrently()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var first = await fixture.AcceptOpeningAsync(Uuid.CreateVersion4());
        var second = await fixture.AcceptOpeningAsync(Uuid.CreateVersion4(), fixture.FirstStart.AddDays(30),
            fixture.FirstEnd.AddDays(30));
        var firstRecord = await fixture.Host.StartProcessAsync(fixture.TenantId, fixture.ProgramId, first, first);
        var secondRecord = await fixture.Host.StartProcessAsync(fixture.TenantId, fixture.ProgramId, second, second);
        var acquireFirst = await fixture.Host.StepAsync(firstRecord, "acquire", nameof(AcquireTypeIIPeriodRange));
        var acquireSecond = await fixture.Host.StepAsync(secondRecord, "acquire", nameof(AcquireTypeIIPeriodRange));

        // Act
        var outcomes = await Task.WhenAll(fixture.Host.SendInNewScopeAsync(acquireFirst),
            fixture.Host.SendInNewScopeAsync(acquireSecond));
        var calendar = await fixture.CalendarAsync();

        // Assert
        Assert.All(outcomes, outcome => Assert.True(outcome.IsSuccess, outcome.Error?.Message));
        Assert.Single(outcomes, outcome => outcome.Value is ReceiverOutcome.Applied);
        Assert.Single(outcomes, outcome => outcome.Value is ReceiverOutcome.Superseded);
        var live = Assert.Single(calendar.Reservations, reservation => reservation.IsLive);
        var loser = live.PeriodId == first ? second : first;
        Assert.IsType<TypeIIPeriodOpeningRejected>((await fixture.PeriodAsync(loser)).TerminalFor(loser));
        Assert.Null((await fixture.PeriodAsync(live.PeriodId)).TerminalFor(live.PeriodId));
    }

    [Fact]
    public async Task ShouldRejectAmendmentAndKeepOldRangeGivenNewRangeOverlappingThirdPeriod()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var first = await fixture.StartPeriodAsync();
        var third = await fixture.StartPeriodAsync(Uuid.CreateVersion4(), fixture.FirstEnd.AddDays(1),
            fixture.FirstEnd.AddYears(1));
        var intoThird = fixture.Amendment(first, 1, null, fixture.FirstEnd.AddDays(20));
        Assert.True((await fixture.AmendAsync(intoThird)).IsSuccess);

        // Act
        var run = await fixture.RunAsync(first, intoThird.CommandId);
        await fixture.Host.ReleaseAsync<TypeIIPeriodAmendmentCanceled>(fixture.StreamOf(first));
        var rejected = await fixture.PeriodAsync(first);
        var calendar = await fixture.CalendarAsync();
        var earlier = fixture.Amendment(first, 1, fixture.FirstStart.AddDays(-7), null);
        var earlierAdmitted = await fixture.AmendAsync(earlier);
        Assert.True(earlierAdmitted.IsSuccess, earlierAdmitted.Error?.Message);
        var earlierRun = await fixture.RunAsync(first, earlier.CommandId);
        var after = await fixture.CalendarAsync();
        var amended = await fixture.PeriodAsync(first);

        // Assert
        Assert.True(earlierAdmitted.IsSuccess, earlierAdmitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Contains("superseded", run.Note, StringComparison.Ordinal);
        var rejection = Assert.IsType<TypeIIPeriodAmendmentCanceled>(rejected.TerminalFor(intoThird.CommandId));
        Assert.Equal(nameof(RejectTypeIIPeriodAmendment), rejection.Operation.CommandKind);
        Assert.Equal(1, rejected.Revision);
        Assert.Equal(fixture.FirstEnd, rejected.View!.PeriodEnd);
        Assert.Equal(TypeIIRangeReservationState.Fenced, calendar.Find(intoThird.CommandId)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, calendar.Find(first)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, calendar.Find(third)!.State);
        Assert.Equal(ProcessState.Completed, earlierRun.State);
        Assert.Equal(2, amended.Revision);
        Assert.Equal(fixture.FirstStart.AddDays(-7), amended.View!.PeriodStart);
        Assert.Equal(TypeIIRangeReservationState.Released, after.Find(first)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, after.Find(earlier.CommandId)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, after.Find(third)!.State);
    }

    [Fact]
    public async Task ShouldFenceDelayedAcquisitionGivenCancellationAndReleaseBeforeAnyAcquire()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var period = await fixture.StartPeriodAsync();
        var request = fixture.Amendment(period, 1, null, fixture.FirstEnd.AddDays(7));
        Assert.True((await fixture.AmendAsync(request)).IsSuccess);
        var record = await fixture.Host.StartProcessAsync(fixture.TenantId, fixture.ProgramId, period,
            request.CommandId);
        Assert.True((await fixture.CancelAsync(fixture.Cancellation(period, request.CommandId, 1))).IsSuccess);

        // Act
        await fixture.Host.ReleaseAsync<TypeIIPeriodAmendmentCanceled>(fixture.StreamOf(period));
        var late = await fixture.Host.SendAsync(await fixture.Host.StepAsync(record, "acquire",
            nameof(AcquireTypeIIPeriodRange)));
        var calendar = await fixture.CalendarAsync();

        // Assert
        Assert.IsType<ReceiverOutcome.Superseded>(late.Value);
        Assert.Equal(TypeIIRangeReservationState.Fenced, calendar.Find(request.CommandId)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, calendar.Find(period)!.State);
        Assert.Single(await fixture.Host.EventsAsync<TypeIIPeriodRangeAuthorized>(fixture.CalendarStream));
    }

    [Fact]
    public async Task ShouldConflictCancellationGivenAmendmentAlreadyApplied()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var period = await fixture.StartPeriodAsync();
        var request = fixture.Amendment(period, 1, null, fixture.FirstEnd.AddDays(7));
        Assert.True((await fixture.AmendAsync(request)).IsSuccess);
        await fixture.RunAsync(period, request.CommandId);

        // Act
        var canceled = await fixture.CancelAsync(fixture.Cancellation(period, request.CommandId, 2));
        var saved = await fixture.PeriodAsync(period);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, canceled.Error?.Kind);
        Assert.IsType<ObservationPeriodAmended>(saved.TerminalFor(request.CommandId));
        Assert.Equal(fixture.FirstEnd.AddDays(7), saved.View!.PeriodEnd);
    }

    [Fact]
    public async Task ShouldReplayCancellationAndConflictGivenRetryWithChangedIntent()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var period = await fixture.StartPeriodAsync();
        var request = fixture.Amendment(period, 1, null, fixture.FirstEnd.AddDays(7));
        Assert.True((await fixture.AmendAsync(request)).IsSuccess);
        var cancel = fixture.Cancellation(period, request.CommandId, 1);
        var first = await fixture.CancelAsync(cancel);
        var before = (await fixture.Host.EventsAsync<DomainEvent>(fixture.StreamOf(period))).Count;

        // Act
        var retry = await fixture.CancelAsync(cancel);
        var changedReason = await fixture.CancelAsync(cancel with { Reason = "A different reason." });
        var changedRevision = await fixture.CancelAsync(cancel with { ExpectedRevision = 0 });
        var otherAmendment = await fixture.CancelAsync(fixture.Cancellation(period, Uuid.CreateVersion4(), 1));

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changedReason.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherAmendment.Error?.Kind);
        Assert.Equal(before, (await fixture.Host.EventsAsync<DomainEvent>(fixture.StreamOf(period))).Count);
    }

    [Fact]
    public async Task ShouldReleaseNothingGivenStaleSettlementProofOrMissingPeriodFact()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var period = await fixture.StartPeriodAsync();
        var request = fixture.Amendment(period, 1, null, fixture.FirstEnd.AddDays(7));
        Assert.True((await fixture.AmendAsync(request)).IsSuccess);
        await fixture.RunAsync(period, request.CommandId);
        var saved = await fixture.PeriodAsync(period);
        var started = Assert.Single(await fixture.Host.EventsAsync<ObservationPeriodStarted>(saved.Stream));
        var amendedIntent = TypeIIPeriod.IntentOf(saved.AcceptedFor(request.CommandId))!;
        var openingIntent = TypeIIPeriod.IntentOf(saved.AcceptedFor(period))!;
        var before = await fixture.CalendarAsync();

        // Act: the stale opening proof arrives after a later amendment released and replaced its range.
        var stale = await fixture.Host.SendAsync(new SettleTypeIIPeriodRange(Uuid.CreateVersion4(),
            NativeFactReference.Of(saved.Stream, saved.FactById(started.Metadata.EventId)!), fixture.CalendarStream,
            period, 1, null, openingIntent, period));
        // Act: a close proof that the period does not hold is not visible, so nothing may be released.
        var missing = await fixture.Host.SendAsync(new SettleTypeIIPeriodRange(Uuid.CreateVersion4(),
            new NativeFactReference(saved.Stream, Uuid.CreateVersion4(), 99), fixture.CalendarStream,
            request.CommandId, 1, null, amendedIntent, period));
        var after = await fixture.CalendarAsync();

        // Assert
        Assert.IsType<ReceiverOutcome.Superseded>(stale.Value);
        Assert.Equal(RequestErrorKind.Conflict, missing.Error?.Kind);
        Assert.True(missing.Error?.IsTransient);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(TypeIIRangeReservationState.Active, after.Find(request.CommandId)!.State);
    }

    [Fact]
    public async Task ShouldNotApplyAndNotForgeRejectionGivenFencedOpeningAndUnprovenConflict()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var first = await fixture.StartPeriodAsync();
        var second = await fixture.AcceptOpeningAsync(Uuid.CreateVersion4());
        var disjoint = await fixture.AcceptOpeningAsync(Uuid.CreateVersion4(), fixture.FirstEnd.AddDays(1),
            fixture.FirstEnd.AddYears(1));
        var secondRecord = await fixture.Host.StartProcessAsync(fixture.TenantId, fixture.ProgramId, second, second);
        await fixture.RunAsync(second, second);
        var calendar = await fixture.CalendarAsync();
        var permit = NativeFactReference.Of(calendar.Stream, calendar.Find(first)!.AuthorizedFact!);
        var disjointPeriod = await fixture.PeriodAsync(disjoint);
        var disjointIntent = TypeIIPeriod.IntentOf(disjointPeriod.AcceptedFor(disjoint))!;
        var forged = await fixture.Host.Content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
            TypeIIRangeFailureProof.Schema, new TypeIIRangeFailureProof(disjointIntent, permit));

        // Act
        var delayed = await fixture.Host.SendAsync(new ApplyTypeIIPeriodRange(Uuid.CreateVersion4(), permit,
            fixture.StreamOf(second), second, 1, null, secondRecord.Intent, second));
        var forgedRejection = await fixture.Host.SendAsync(new RejectTypeIIPeriodOpening(Uuid.CreateVersion4(), disjoint,
            fixture.StreamOf(disjoint), 1, 0, forged));
        var unchanged = await fixture.PeriodAsync(disjoint);

        // Assert
        Assert.IsType<ReceiverOutcome.Superseded>(delayed.Value);
        Assert.Empty(await fixture.Host.EventsAsync<ObservationPeriodStarted>(fixture.StreamOf(second)));
        Assert.Equal(RequestErrorKind.Conflict, forgedRejection.Error?.Kind);
        Assert.False(forgedRejection.Error?.IsTransient);
        Assert.NotNull(unchanged.Pending);
        Assert.Null(unchanged.TerminalFor(disjoint));
    }

    [Fact]
    public async Task ShouldRegisterOneProcessDefinitionEqualToCatalogGivenComposition()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();

        // Act
        var definition = fixture.Host.Definition;
        var registrations = fixture.Host.Provider.GetServices<WorkloadRegistration>().ToArray();
        var root = Assert.Single(registrations, item => item.Name == TypeIIRangeReactor.WorkloadName);
        var release = Assert.Single(registrations, item => item.Name == TypeIIRangeReleaseReactor.WorkloadName);
        var pattern = EventStreamPattern.ForTenant(TypeIIPeriod.Area);

        // Assert
        Assert.True(definition.Ordered);
        Assert.Equal(["TypeIIPeriodOpeningAccepted", "TypeIIPeriodAmendmentAccepted"], definition.RootSourceEvents);
        Assert.Equal(["acquire", "apply", "settle"], definition.Stages.Select(stage => stage.Stage));
        Assert.Equal([["AcquireTypeIIPeriodRange"], ["ApplyTypeIIPeriodRange"], ["SettleTypeIIPeriodRange"]],
            definition.Stages.Select(stage => stage.Receivers));
        Assert.Equal(["RejectTypeIIPeriodOpening", "RejectTypeIIPeriodAmendment", "CancelTypeIIPeriodAmendment"],
            definition.Abort);
        Assert.Equal(pattern, ((Reactor)fixture.Host.Services.GetRequiredService(root.ComponentType)).Pattern);
        Assert.Equal(pattern, ((Reactor)fixture.Host.Services.GetRequiredService(release.ComponentType)).Pattern);
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
