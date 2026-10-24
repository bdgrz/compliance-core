using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class TypeIIPeriodCommandHandlerTests
{
    static readonly DateTimeOffset StartedAt = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset AmendedAt = StartedAt.AddMonths(2);

    [Fact]
    public async Task ShouldPreserveUnchangedBoundaryGivenStartedPeriodWhenAmendingOnlyPeriodEnd()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var request = fixture.Amendment(1, null, fixture.PeriodEnd.AddDays(7), "Extend for the delayed sample window.");

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var pending = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));
        await fixture.RunProcessAsync(request.CommandId);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(request.CommandId, result.Value!.OperationId);
        Assert.Equal(1, pending.Revision);
        Assert.Equal(fixture.PeriodEnd, pending.Current!.PeriodEnd);
        Assert.Equal(request.CommandId, pending.PendingOperationId);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(fixture.PeriodStart, saved.Current!.PeriodStart);
        Assert.Equal(fixture.PeriodEnd.AddDays(7), saved.Current.PeriodEnd);
        Assert.Equal("Extend for the delayed sample window.", Assert.Single(saved.Current.Amendments).Reason);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.ActorUserId), saved.Current.ActorMemberId);
    }

    [Fact]
    public async Task ShouldHoldBothRangesThenReleaseOldRangeGivenAmendmentProcess()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var amendedEnd = fixture.PeriodEnd.AddDays(7);
        var request = fixture.Amendment(1, null, amendedEnd, "Extend for the delayed sample window.");

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var retry = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var run = await fixture.RunProcessAsync(request.CommandId);
        var replayed = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var calendar = await fixture.Reader.HydrateAsync(new TypeIIObservationCalendar(
            fixture.TenantId, fixture.ProgramId));
        var period = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(result.Value, retry.Value);
        Assert.Equal(result.Value, replayed.Value);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(2, period.Revision);
        Assert.Equal(4, calendar.Revision);
        Assert.Equal(TypeIIRangeReservationState.Released, calendar.Find(fixture.PeriodId)!.State);
        var live = calendar.Find(request.CommandId)!;
        Assert.Equal(TypeIIRangeReservationState.Active, live.State);
        Assert.Equal(new ObservationRange(fixture.PeriodStart, amendedEnd), live.Range);
        Assert.Equal(new TypeIIPeriodCalendarAuthorization(request.CommandId, live.Generation),
            period.CalendarAuthorization);
    }

    [Fact]
    public async Task ShouldRejectStaleAmendmentWithoutAppendingGivenConcurrentPeriodChange()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var first = fixture.Amendment(1, null, fixture.PeriodEnd.AddDays(7), "Extend the period.");
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(first), CancellationToken.None)).IsSuccess);
        await fixture.RunProcessAsync(first.CommandId);
        var stale = fixture.Amendment(1, fixture.PeriodStart.AddDays(1), null, "Move the start date.");

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(stale), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(fixture.PeriodStart, saved.Current!.PeriodStart);
        Assert.Equal(fixture.PeriodEnd.AddDays(7), saved.Current.PeriodEnd);
        Assert.Single(saved.Current.Amendments);
        Assert.Null(saved.Pending);
    }

    [Fact]
    public async Task ShouldConflictGivenSameAmendmentIdentityWithChangedIntent()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var request = fixture.Amendment(1, null, fixture.PeriodEnd.AddDays(7), "Extend the period.");
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);

        // Act
        var changedDates = await fixture.Handler.HandleAsync(fixture.Context(request with
        {
            PeriodEnd = fixture.PeriodEnd.AddDays(8),
        }), CancellationToken.None);
        var changedRevision = await fixture.Handler.HandleAsync(fixture.Context(request with { ExpectedRevision = 0 }),
            CancellationToken.None);
        var otherAmendment = await fixture.Handler.HandleAsync(fixture.Context(fixture.Amendment(1, null,
            fixture.PeriodEnd.AddDays(9), "A second amendment while one is pending.")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changedDates.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherAmendment.Error?.Kind);
        Assert.True(otherAmendment.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldApproveExactCloseWithExplicitAcknowledgementsGivenCurrentCisoPermission()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCloseReadyPeriodAsync();
        var request = new ApproveTypeIIPeriodClose(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, 2, "Reviewed the reconciled period and unresolved material item.",
            [fixture.GapId]);

        // Act
        var result = await fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso)
            .HandleAsync(fixture.Context(request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("close_approved", saved.Current!.Status);
        var approval = Assert.Single(saved.Current.CloseApprovals);
        Assert.Equal(fixture.ActorMemberId, approval.ApprovedByMemberId);
        Assert.Equal("ciso", approval.ApproverRole);
        Assert.Equal(fixture.GapId, Assert.Single(approval.AcknowledgedMaterialItemIds));
    }

    [Fact]
    public async Task ShouldRejectTokenExecutiveRoleGivenNoCurrentExecutivePermission()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCloseReadyPeriodAsync();
        var request = new ApproveTypeIIPeriodClose(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, 2, "A token claim cannot authorize period close.", [fixture.GapId]);

        // Act
        var result = await fixture.ApproveHandler().HandleAsync(
            fixture.Context(request, "ceo"), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.CloseApprovals);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldTreatSameCloseApprovalAsIdempotentGivenLaterClockTime()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCloseReadyPeriodAsync();
        var handler = fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso);
        var request = new ApproveTypeIIPeriodClose(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, 2, "Reviewed the reconciled period and unresolved material item.",
            [fixture.GapId]);
        var context = fixture.Context(request);
        Assert.True((await handler.HandleAsync(context, CancellationToken.None)).IsSuccess);
        fixture.CloseClock.Set(fixture.CloseClock.GetUtcNow().AddHours(2));

        // Act
        var retry = await handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(3, saved.Revision);
        var approval = Assert.Single(saved.Current!.CloseApprovals);
        Assert.Equal(fixture.CloseValidationAt.AddHours(1), approval.ApprovedAt);
    }

    [Fact]
    public async Task ShouldFreezeExactApprovedCloseNamingLiveReservationAndReleaseItGivenAuthorizedExecutive()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCloseReadyPeriodAsync();
        var approved = await fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso)
            .HandleAsync(fixture.Context(new ApproveTypeIIPeriodClose(fixture.TenantId,
                fixture.ProgramId, fixture.PeriodId, 2,
                "Approved the reconciled period.", [fixture.GapId])), CancellationToken.None);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        var closeSnapshotId = Uuid.CreateVersion4();
        var request = new FreezeTypeIIPeriodClose(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, closeSnapshotId, 3);

        // Act
        var result = await fixture.FreezeHandler(RbacPermissions.ManagementExecutiveCiso)
            .HandleAsync(fixture.Context(request), CancellationToken.None);
        var period = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));
        var calendarBefore = await fixture.Reader.HydrateAsync(new TypeIIObservationCalendar(
            fixture.TenantId, fixture.ProgramId));
        await fixture.Host.ReleaseAsync<ObservationPeriodCloseFrozen>(period.Stream);
        await fixture.Host.ReleaseAsync<ObservationPeriodCloseFrozen>(period.Stream);
        var calendar = await fixture.Reader.HydrateAsync(new TypeIIObservationCalendar(
            fixture.TenantId, fixture.ProgramId));
        var frozen = Assert.Single(await fixture.Host.EventsAsync<ObservationPeriodCloseFrozen>(period.Stream));
        var settlements = await fixture.Host.EventsAsync<TypeIIPeriodRangeSettled>(calendar.Stream);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = Assert.Single(period.Current!.CloseSnapshots);
        Assert.Equal(closeSnapshotId, snapshot.CloseSnapshotId);
        Assert.Equal("closed", period.Current.Status);
        Assert.Equal(fixture.PeriodId, frozen.CalendarAuthorizationId);
        Assert.Equal(1, frozen.CalendarGeneration);
        Assert.Equal(TypeIIRangeReservationState.Active, calendarBefore.Find(fixture.PeriodId)!.State);
        Assert.Equal(TypeIIRangeReservationState.Released, calendar.Find(fixture.PeriodId)!.State);
        Assert.Equal(3, calendar.Revision);
        var release = settlements.Last();
        Assert.Equal(TypeIIRangeSettlementKind.Released, release.Settlement);
        Assert.Equal(fixture.PeriodId, release.TargetOperationId);
    }

    [Fact]
    public async Task ShouldReplayFreezeWithoutAppendingGivenCloseFreezeRetryAfterClockAdvances()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCloseReadyPeriodAsync();
        var approved = await fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso)
            .HandleAsync(fixture.Context(new ApproveTypeIIPeriodClose(fixture.TenantId,
                fixture.ProgramId, fixture.PeriodId, 2,
                "Approved the reconciled period.", [fixture.GapId])), CancellationToken.None);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        var request = new FreezeTypeIIPeriodClose(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, Uuid.CreateVersion4(), 3);
        var handler = fixture.FreezeHandler(RbacPermissions.ManagementExecutiveCiso);
        var context = fixture.Context(request);
        Assert.True((await handler.HandleAsync(context, CancellationToken.None)).IsSuccess);
        fixture.CloseClock.Set(fixture.CloseClock.GetUtcNow().AddDays(1));

        // Act
        var retry = await handler.HandleAsync(context, CancellationToken.None);
        var period = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(4, period.Revision);
        Assert.Single(period.Current!.CloseSnapshots);
    }

    [Fact]
    public async Task ShouldRequireCurrentExecutivePermissionGivenApprovedClose()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCloseReadyPeriodAsync();
        var approved = await fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso)
            .HandleAsync(fixture.Context(new ApproveTypeIIPeriodClose(fixture.TenantId,
                fixture.ProgramId, fixture.PeriodId, 2,
                "Approved the reconciled period.", [fixture.GapId])), CancellationToken.None);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        var request = new FreezeTypeIIPeriodClose(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, Uuid.CreateVersion4(), 3);

        // Act
        var result = await fixture.FreezeHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var period = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("close_approved", period.Current!.Status);
        Assert.Empty(period.Current.CloseSnapshots);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ClaimsPrincipal _actor;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid PlanId { get; } = Uuid.CreateVersion4();
        public Uuid PeriodId { get; } = Uuid.CreateVersion4();
        public Uuid ActorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ProposerMemberId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerMemberId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverMemberId { get; } = Uuid.CreateVersion4();
        public Uuid ValidatorMemberId { get; } = Uuid.CreateVersion4();
        public Uuid ControlOwnerId { get; } = Uuid.CreateVersion4();
        public Uuid GapId { get; } = Uuid.CreateVersion4();
        public DateOnly PeriodStart { get; } = new(2026, 10, 1);
        public DateOnly PeriodEnd { get; } = new(2027, 9, 30);
        public Uuid ActorMemberId => RbacIds.Member(TenantId, ActorUserId);
        public DateTimeOffset CloseValidationAt { get; } = StartedAt.AddMonths(12);
        public MutableTimeProvider CloseClock { get; }
        public TypeIIRangeHost Host { get; }
        public IAggregateExecutor Executor => Host.Executor;
        public IAggregateWriter Writer => Host.Writer;
        public IAggregateReader Reader => Host.Reader;
        public IDomainEventReader Events => Host.Events;
        public AmendTypeIIObservationPeriodHandler Handler { get; }
        public TimeProvider Clock { get; } = new FixedTimeProvider(AmendedAt);

        public Fixture()
        {
            _actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", ActorUserId.ToString()),
                    new Claim("name", "Period manager")], "BdgrzSession"));
            Host = new TypeIIRangeHost(Clock);
            Handler = Host.AmendHandler(Clock);
            CloseClock = new MutableTimeProvider(CloseValidationAt.AddHours(1));
        }

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(_actor, new HttpInvocation("POST", "/type-ii-periods/amend",
                "/type-ii-periods/amend", "period-amendment-test")));

        public Cntryl.Portia.RequestContext<T> Context<T>(T request, string roleClaim)
            where T : IRequestBase
        {
            var actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", ActorUserId.ToString()),
                    new Claim("name", "Period manager"), new Claim(ClaimTypes.Role, roleClaim)],
                "BdgrzSession"));
            return new Cntryl.Portia.RequestContext<T>(request,
                new RequestDispatchContext(actor, new HttpInvocation("POST", "/type-ii-periods/approve",
                    "/type-ii-periods/approve", "period-close-approval-test")));
        }

        public ApproveTypeIIPeriodCloseHandler ApproveHandler(params string[] permissions) =>
            new(Executor, new ManagementExecutiveAuthority(Reader,
                new ExecutivePermissions(permissions)), CloseClock);

        public FreezeTypeIIPeriodCloseHandler FreezeHandler(params string[] permissions) =>
            new(Executor, Reader, new ManagementExecutiveAuthority(Reader,
                new ExecutivePermissions(permissions)), CloseClock);

        public AmendTypeIIObservationPeriod Amendment(long expectedRevision, DateOnly? start, DateOnly? end,
            string reason) => new(Uuid.CreateVersion4(), TenantId, ProgramId, PeriodId, expectedRevision, start, end,
            reason);

        /// <summary>Starts (if absent) and drives the range process of an admitted amendment to its end.</summary>
        public Task<ProcessRecord> RunProcessAsync(Uuid operationId) =>
            Host.RunProcessAsync(TenantId, ProgramId, PeriodId, operationId);

        public async Task RegisterActorAsync()
        {
            var member = new Member(TenantId, ActorUserId);
            Assert.True(member.Register().IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public async Task SeedStartedPeriodAsync()
        {
            var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
            Assert.Null(plan.Propose(PlanContent(), ProposerMemberId, "Plan proposer", StartedAt.AddDays(-3)));
            Assert.Null(plan.Review(plan.Revision, 1, "approve", "Exact controls and period reviewed.",
                ReviewerMemberId, "Independent reviewer", StartedAt.AddDays(-2)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved for observation.", ApproverMemberId,
                "CISO", "Executive approver", StartedAt.AddDays(-1)));
            await Writer.SaveAsync(plan, new RequestDispatchContext(RequestActor.System));

            var period = new TypeIIPeriod(TenantId, ProgramId, PeriodId);
            var calendar = new TypeIIObservationCalendar(TenantId, ProgramId);
            var header = new DurableIntentHeader(PeriodId, period.Stream, period.Stream, period.Stream,
                ActorReference.ForMember(ProposerMemberId, "Compliance lead"), "initial", null, new string('b', 64),
                StartedAt);
            var intent = await Host.Content.WriteJsonAsync(TenantId.ToString(), Uuid.CreateVersion4(),
                TypeIIRangeIntent.Schema, new TypeIIRangeIntent(header, ProgramId, PeriodId,
                    TypeIIRangeOperationKind.Opening, null, null, new ObservationRange(PeriodStart, PeriodEnd),
                    new ImmutableContentRef(Uuid.CreateVersion4(), new string('a', 64), 2, "application/json",
                        TypeIIPeriodOpeningChange.Schema),
                    TypeIIRangeEffects.KeyOf(PeriodId, period.Stream, calendar.Stream)));
            Assert.Null(period.Start(plan.Current!, Opening(), ProposerMemberId, "Compliance lead", StartedAt,
                intent));
            await Writer.SaveAsync(period, new RequestDispatchContext(RequestActor.System));

            // The calendar holds the range the opening was applied with, exactly as the range process leaves it.
            var provenance = new OperationProvenance(Uuid.CreateVersion4(), PeriodId, null, "SeedRange",
                ActorReference.ForMember(ProposerMemberId, "Compliance lead"), calendar.Stream, "initial", null,
                new string('b', 64), StartedAt, calendar.Stream, calendar.Stream);
            Assert.True(calendar.Authorize(provenance, intent, PeriodId, TypeIIRangeOperationKind.Opening,
                new ObservationRange(PeriodStart, PeriodEnd), null).IsSuccess);
            Assert.True(calendar.Settle(provenance with { LogicalId = Uuid.CreateVersion4() }, intent, PeriodId,
                TypeIIRangeSettlementKind.Activated, PeriodId, null).IsSuccess);
            await Writer.SaveAsync(calendar, new RequestDispatchContext(RequestActor.System));
        }

        public async Task SeedCloseReadyPeriodAsync()
        {
            await SeedStartedPeriodAsync();
            var period = await Reader.HydrateAsync(new TypeIIPeriod(TenantId, ProgramId, PeriodId));
            var occurrenceIds = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId)
                .ToArray();
            Assert.Null(period.RecordCloseValidation(period.Revision,
                new TypeIIPeriodCloseValidationContent(ValidationReferences(), occurrenceIds,
                    occurrenceIds, [], [], [GapId]), ValidatorMemberId, "Close validator",
                CloseValidationAt));
            await Writer.SaveAsync(period, new RequestDispatchContext(RequestActor.System));
        }

        TypeIIPlanContent PlanContent() => new(ExaminationId, Uuid.CreateVersion4(), new string('a', 64),
            Uuid.CreateVersion4(), PeriodStart, PeriodEnd, "Audit Firm", ["security", "availability"],
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), [Reference("policy"), Reference("risk"),
                Reference("vendor"), Reference("finding"), Reference("change")],
            [new TypeIIControlPlanEntry(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                "Quarterly access review", ControlOwnerId, "quarterly", 4, PeriodStart, null,
                ExpectedEvidenceDescriptions: ["Dated review record"])],
            [new TypeIICarriedForwardGap("finding", GapId, Uuid.CreateVersion4(), 1,
                new string('b', 64), "carry")],
            [new TypeIIPlanGapAcknowledgement(GapId, "Open control gap", "acknowledged",
                "The control owner and due date were confirmed.")], []);

        TypeIIPeriodOpeningContent Opening() => new(ExaminationId, Uuid.CreateVersion4(),
            new string('c', 64), [Reference("boundary"), Reference("inventory"), Reference("control"),
                Reference("policy"), Reference("provider"), Reference("finding")], [GapId]);

        static ExaminationContentReference Reference(string sourceType) => new(sourceType,
            Uuid.CreateVersion5(Uuid.Empty, "type-ii-period-source-record:" + sourceType),
            Uuid.CreateVersion5(Uuid.Empty, "type-ii-period-source-version:" + sourceType), 1,
            new string('d', 64));

        static ExaminationContentReference[] ValidationReferences() =>
            [Reference("boundary"), Reference("inventory"), Reference("control"),
                Reference("policy"), Reference("provider"), Reference("finding")];

        public ValueTask DisposeAsync() => Host.DisposeAsync();
    }

    sealed class ExecutivePermissions(params string[] permissions)
        : IAccessGrantPermissionAuthorizer
    {
        readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.Ordinal);

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(_permissions.Contains(permission));

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(false, new HashSet<Uuid>()));
    }

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Set(DateTimeOffset value) => _utcNow = value;
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
