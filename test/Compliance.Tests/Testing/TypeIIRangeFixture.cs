using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds programs with Type II periods through the real admission facts: the opening is accepted on the period
///     with its complete persisted intent and then driven through <c>TypeIIRangeProcess</c>, so the calendar holds
///     genuine authorizations with committed metadata. Amendments and cancellations go through the real handlers.
/// </summary>
sealed class TypeIIRangeFixture : IAsyncDisposable
{
    public static readonly DateTimeOffset StartedAt = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    readonly ClaimsPrincipal _actor;

    public TypeIIRangeFixture(Func<IProcessRecordStore, IProcessRecordStore>? store = null)
    {
        _actor = TypeIIRangeHost.Personal(ActorUserId);
        Clock = new ManualClock(StartedAt);
        Host = new TypeIIRangeHost(Clock, store);
    }

    /// <summary>Seeds a running application's stores for one tenant and the user that calls its HTTP routes.</summary>
    public TypeIIRangeFixture(IServiceProvider services, Uuid tenantId, Uuid actorUserId)
    {
        (TenantId, ActorUserId) = (tenantId, actorUserId);
        _actor = TypeIIRangeHost.Personal(ActorUserId);
        Clock = new ManualClock(StartedAt);
        Host = new TypeIIRangeHost(services);
    }

    public Uuid TenantId { get; } = Uuid.CreateVersion4();

    public Uuid ProgramId { get; } = Uuid.CreateVersion4();

    public Uuid ExaminationId { get; } = Uuid.CreateVersion4();

    public Uuid ActorUserId { get; } = Uuid.CreateVersion4();

    public Uuid ActorMemberId => RbacIds.Member(TenantId, ActorUserId);

    public Uuid FirstPeriodId { get; } = Uuid.CreateVersion4();

    /// <summary>The control the period plan observes, planned quarterly from the start of the first period.</summary>
    public Uuid ControlId { get; } = Uuid.CreateVersion4();

    public Uuid ControlVersionId { get; } = Uuid.CreateVersion4();

    public Uuid OwnerMemberId { get; } = Uuid.CreateVersion4();

    public Uuid ReviewerMemberId { get; } = Uuid.CreateVersion4();

    public DateOnly FirstStart { get; } = new(2026, 10, 1);

    public DateOnly FirstEnd { get; } = new(2027, 9, 30);

    public ManualClock Clock { get; }

    public TypeIIRangeHost Host { get; }

    public EventStreamAddress CalendarStream => new TypeIIObservationCalendar(TenantId, ProgramId).Stream;

    public EventStreamAddress StreamOf(Uuid periodId) => new TypeIIPeriod(TenantId, ProgramId, periodId).Stream;

    public Task<TypeIIPeriodReadSnapshot> PeriodAsync(Uuid periodId) =>
        Host.Periods.ReadAsync(TenantId, ProgramId, periodId).AsTask();

    public Task<TypeIIObservationCalendar> CalendarAsync() =>
        Host.HydrateAsync(new TypeIIObservationCalendar(TenantId, ProgramId));

    public Task<ProcessRecord> RunAsync(Uuid periodId, Uuid operationId) =>
        Host.RunProcessAsync(TenantId, ProgramId, periodId, operationId);

    public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
        new RequestDispatchContext(_actor, new HttpInvocation("PUT", "/type-ii-periods", "/type-ii-periods",
            "type-ii-range-test")));

    public AmendTypeIIObservationPeriod Amendment(Uuid periodId, long expectedRevision, DateOnly? start, DateOnly? end,
        string reason = "Align the observation window.") =>
        new(Uuid.CreateVersion4(), TenantId, ProgramId, periodId, expectedRevision, start, end, reason);

    public Task<Result<OperationAcceptedResponse>> AmendAsync(AmendTypeIIObservationPeriod request) =>
        Host.AmendHandler(Clock).HandleAsync(Context(request), CancellationToken.None).AsTask();

    public Task<Result<UnitResult>> CancelAsync(CancelTypeIIPeriodAmendment request) =>
        Host.CancelHandler(Clock).HandleAsync(Context(request), CancellationToken.None).AsTask();

    public CancelTypeIIPeriodAmendment Cancellation(Uuid periodId, Uuid amendmentId, long expectedRevision,
        string reason = "The window is no longer needed.") =>
        new(Uuid.CreateVersion4(), TenantId, ProgramId, periodId, amendmentId, expectedRevision, reason);

    /// <summary>Accepts an opening with its complete persisted intent and range, without touching the calendar.</summary>
    public async Task<Uuid> AcceptOpeningAsync(Uuid? periodId = null, DateOnly? start = null, DateOnly? end = null)
    {
        var id = periodId ?? FirstPeriodId;
        var (from, until) = (start ?? FirstStart, end ?? FirstEnd);
        var plan = new TypeIIPlanAggregate(TenantId, ProgramId, Uuid.CreateVersion4());
        var gap = Uuid.CreateVersion4();
        Assert.Null(plan.Propose(PlanContent(from, until, gap), Uuid.CreateVersion4(), "Plan proposer",
            StartedAt.AddDays(-3)));
        Assert.Null(plan.Review(plan.Revision, 1, "approve", "Exact controls and period reviewed.",
            Uuid.CreateVersion4(), "Independent reviewer", StartedAt.AddDays(-2)));
        Assert.Null(plan.Approve(plan.Revision, 1, "Approved for observation.", Uuid.CreateVersion4(), "CISO",
            "Executive approver", StartedAt.AddDays(-1)));
        await Host.Writer.SaveAsync(plan, new RequestDispatchContext(RequestActor.System));

        var period = new TypeIIPeriod(TenantId, ProgramId, id, id);
        Assert.Null(new TypeIIPeriodRules(TenantId, ProgramId, id).PrepareOpening(plan.Current!, Opening(gap), ActorMemberId, "Compliance lead", StartedAt,
            out var initial));
        var realm = TenantId.ToString();
        var change = await Host.Content.WriteJsonAsync(realm, Uuid.CreateVersion4(), TypeIIPeriodOpeningChange.Schema,
            new TypeIIPeriodOpeningChange(initial!));
        var header = new DurableIntentHeader(id, period.Stream, period.Stream, period.Stream,
            ActorReference.ForMember(ActorMemberId, "Compliance lead"), "initial", null, new string('b', 64),
            StartedAt);
        var intent = await Host.Content.WriteJsonAsync(realm, Uuid.CreateVersion4(), TypeIIRangeIntent.Schema,
            new TypeIIRangeIntent(header, ProgramId, id, TypeIIRangeOperationKind.Opening, null, null,
                new ObservationRange(from, until), change, TypeIIRangeEffects.KeyOf(id, period.Stream, CalendarStream)));
        var response = await TypeIIRangeContent.AcceptedResponseAsync(Host.Content, realm, id, StartedAt, CancellationToken.None);
        var firstResponse = await FirstResponses.FreezeAsync(Host.Content, realm, response);
        var provenance = new OperationProvenance(id, id, null, "StartTypeIIObservationPeriod",
            ActorReference.ForMember(ActorMemberId, "Compliance lead"), period.Stream, "initial", null,
            new string('b', 64), StartedAt, period.Stream, period.Stream);
        Assert.True(period.AcceptOpening(provenance, intent, firstResponse).IsSuccess);
        await Host.Writer.SaveAsync(period, new RequestDispatchContext(RequestActor.System));
        return id;
    }

    /// <summary>Accepts an opening and runs its range process to the end, leaving the period started and its range active.</summary>
    public async Task<Uuid> StartPeriodAsync(Uuid? periodId = null, DateOnly? start = null, DateOnly? end = null)
    {
        var id = await AcceptOpeningAsync(periodId, start, end);
        var run = await RunAsync(id, id);
        Assert.Equal(ProcessState.Completed, run.State);
        return id;
    }

    /// <summary>Seeds the approved quarterly operating plan of the period's control, effective from the period start.</summary>
    public async Task<ControlOperatingPlanView> SeedOperatingPlanAsync()
    {
        var planId = Uuid.CreateVersion4();
        await ControlOperatingSeed.ProposeAsync(Host.Provider, TenantId, ProgramId, ControlId, ControlVersionId,
            ["Dated review record"], planId, new OperatingHolder("member", OwnerMemberId), null, ReviewerMemberId,
            new ControlCadence(ControlCadenceSchedule.Recurring, "quarterly", FirstStart, 0), FirstStart,
            "Operate quarterly.", ActorMemberId, "Compliance lead", StartedAt.AddDays(-2));
        return await ControlOperatingSeed.ApproveAsync(Host.Provider, TenantId, ProgramId, ControlId, planId,
            Uuid.CreateVersion4(), "Approver", StartedAt.AddDays(-1));
    }

    /// <summary>Records, approves and freezes the close of a period, as the executive handlers leave it.</summary>
    public async Task FreezeCloseAsync(Uuid periodId)
    {
        var period = (await PeriodAsync(periodId)).Rules;
        var references = period.Current!.OpeningSourceReferences.ToArray();
        var expected = period.Current.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
        var at = StartedAt.AddMonths(30);
        Assert.Null(period.RecordCloseValidation(period.Revision,
            new TypeIIPeriodCloseValidationContent(references, expected, expected, [], [], []), Uuid.CreateVersion4(),
            "Close validator", at));
        Assert.Null(period.ApproveClose(period.Revision, [], "Reviewed the complete period.", Uuid.CreateVersion4(),
            "CEO", "Chief Executive", at.AddHours(1)));
        var approver = period.Current!.CloseApprovals[^1].ApprovedByMemberId;
        Assert.Null(period.FreezeClose(period.Revision, new TypeIIPeriodCloseSnapshotContent(Uuid.CreateVersion4(),
            references, expected, []), approver, "Chief Executive", at.AddHours(2)));
        await TypeIIPeriodSeeds.AppendAsync(Host.Reader, Host.Writer, Host.Content, period);
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    TypeIIPlanContent PlanContent(DateOnly start, DateOnly end, Uuid gap) => new(ExaminationId,
        Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(), start, end, "Audit Firm",
        ["security", "availability"], Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        [Reference("policy"), Reference("risk"), Reference("vendor"), Reference("finding"), Reference("change")],
        [new TypeIIControlPlanEntry(ControlId, ControlVersionId, "Quarterly access review",
            OwnerMemberId, "quarterly", 4, start, null,
            ExpectedEvidenceDescriptions: ["Dated review record"])],
        [new TypeIICarriedForwardGap("finding", gap, Uuid.CreateVersion4(), 1, new string('b', 64), "carry")],
        [new TypeIIPlanGapAcknowledgement(gap, "Open control gap", "acknowledged",
            "The control owner and due date were confirmed.")], []);

    TypeIIPeriodOpeningContent Opening(Uuid gap) => new(ExaminationId, Uuid.CreateVersion4(), new string('c', 64),
        [Reference("boundary"), Reference("inventory"), Reference("control"), Reference("policy"),
            Reference("provider"), Reference("finding")], [gap]);

    static ExaminationContentReference Reference(string sourceType) => new(sourceType,
        Uuid.CreateVersion5(Uuid.Empty, "type-ii-period-source-record:" + sourceType),
        Uuid.CreateVersion5(Uuid.Empty, "type-ii-period-source-version:" + sourceType), 1, new string('d', 64));

    /// <summary>A settable clock.</summary>
    public sealed class ManualClock(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Set(DateTimeOffset value) => _utcNow = value;
    }
}
