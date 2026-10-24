using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Tests.Features.Examinations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditOutcomeAggregate = Bdgrz.Compliance.Features.Examinations.AuditOutcome;
using ExaminationEngagementAggregate = Bdgrz.Compliance.Features.Examinations.ExaminationEngagement;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescription;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class StartTypeIIObservationPeriodHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    static readonly string[] RequiredSections =
    [
        "services", "commitments_requirements", "components_infrastructure_software", "people",
        "procedures", "information_data_flows", "system_boundary", "control_environment",
        "user_entity_controls", "subservice_organizations", "exclusions", "relevant_changes",
    ];

    [Fact]
    public async Task ShouldPersistOpeningIntentBeforeCalendarAndStartAfterProcessGivenPersonalActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync();
        var request = fixture.Request();

        // Act
        var admitted = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var accepted = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId));
        var calendarBefore = await fixture.Reader.HydrateAsync(new TypeIIObservationCalendar(fixture.TenantId,
            fixture.ProgramId));
        var run = await fixture.RunProcessAsync();
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId));
        var calendar = await fixture.Reader.HydrateAsync(new TypeIIObservationCalendar(fixture.TenantId,
            fixture.ProgramId));

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(fixture.PeriodId, admitted.Value!.OperationId);
        Assert.Equal(0, accepted.Revision);
        Assert.NotNull(accepted.Pending);
        Assert.Equal(0, calendarBefore.Revision);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(fixture.ExaminationId, saved.Current!.ExaminationId);
        Assert.Equal(fixture.PlanId, saved.Current.TypeIIPlanId);
        Assert.Equal(fixture.BaselineSources.Select(fixture.ToReference), saved.Current.OpeningSourceReferences);
        Assert.Equal(fixture.PeriodId, saved.Current.OpeningSnapshotId);
        Assert.Equal(64, saved.Current.OpeningSnapshotSha256.Length);
        var reservation = calendar.Find(fixture.PeriodId)!;
        Assert.Equal(TypeIIRangeReservationState.Active, reservation.State);
        Assert.Equal(new ObservationRange(fixture.PeriodStart, fixture.PeriodEnd), reservation.Range);
        Assert.Equal(new TypeIIPeriodCalendarAuthorization(fixture.PeriodId, reservation.Generation),
            saved.CalendarAuthorization);
    }

    [Fact]
    public async Task ShouldScheduleExpectedOccurrencesAfterApprovedCadenceDueWindowGivenPeriodStart()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync();

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request()), CancellationToken.None);
        await fixture.RunProcessAsync();
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([
            new DateOnly(2027, 1, 5),
            new DateOnly(2027, 4, 5),
            new DateOnly(2027, 7, 5),
            new DateOnly(2027, 10, 5),
        ], saved.Current!.ExpectedOccurrences.Select(item => item.DueOn));
        Assert.All(saved.Current.ExpectedOccurrences, item =>
            Assert.Equal(["Dated review record"], item.ExpectedEvidenceDescriptions));
    }

    [Fact]
    public async Task ShouldRejectPlanBoundToDifferentTypeIBaselineGivenApprovedPlan()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync(sourceSnapshotId: Uuid.CreateVersion4());

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request()),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
        Assert.Null(saved.Pending);
    }

    [Fact]
    public async Task ShouldRejectControlVersionOutsideTypeIBaselineGivenApprovedPlan()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync(controlVersionId: Uuid.CreateVersion4());

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request()),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
        Assert.Null(saved.Pending);
    }

    [Fact]
    public async Task ShouldReplayOriginalAcknowledgmentAndPreserveStartTimeGivenSameIntentRetriedLater()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync();
        var context = fixture.Context(fixture.Request());
        var first = await fixture.Handler.HandleAsync(context, CancellationToken.None);
        await fixture.RunProcessAsync();
        fixture.Clock.Set(Now.AddHours(1));
        var stream = new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId).Stream;
        var before = (await fixture.Host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var retry = await fixture.Handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(Now, saved.Current!.UpdatedAt);
        Assert.Equal(before, (await fixture.Host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldConflictGivenSamePeriodIdentityWithChangedIntent()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync();
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(fixture.Request()),
            CancellationToken.None)).IsSuccess);
        var stream = new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId).Stream;
        var before = (await fixture.Host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var changed = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request() with
        {
            PeriodEnd = fixture.PeriodEnd.AddDays(-1),
        }), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(before, (await fixture.Host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectSecondOpeningAndKeepFirstRangeGivenOverlappingProgramPeriod()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync();
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(fixture.Request()),
            CancellationToken.None)).IsSuccess);
        await fixture.RunProcessAsync();
        var secondPeriodId = Uuid.CreateVersion4();
        var secondRequest = fixture.Request() with { PeriodId = secondPeriodId };

        // Act
        var admitted = await fixture.Handler.HandleAsync(fixture.Context(secondRequest), CancellationToken.None);
        var run = await fixture.RunProcessAsync(secondPeriodId);
        var second = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId,
            secondPeriodId));
        var calendar = await fixture.Reader.HydrateAsync(new TypeIIObservationCalendar(fixture.TenantId,
            fixture.ProgramId));

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Contains("superseded", run.Note, StringComparison.Ordinal);
        Assert.Equal(0, second.Revision);
        Assert.IsType<TypeIIPeriodOpeningRejected>(second.TerminalFor(secondPeriodId));
        Assert.Null(second.Pending);
        var live = Assert.Single(calendar.Reservations, reservation => reservation.IsLive);
        Assert.Equal(fixture.PeriodId, live.PeriodId);
        Assert.Null(calendar.Find(secondPeriodId));
    }

    [Fact]
    public async Task ShouldAllowDateReuseGivenCloseFreezeReleasedTheRange()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync();
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(fixture.Request()),
            CancellationToken.None)).IsSuccess);
        await fixture.RunProcessAsync();
        await fixture.FreezeCloseAsync();
        var blockedId = Uuid.CreateVersion4();
        var blocked = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request() with
        {
            PeriodId = blockedId,
        }), CancellationToken.None);
        await fixture.RunProcessAsync(blockedId);
        var stillBlocked = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId,
            blockedId));
        var reopenedId = Uuid.CreateVersion4();

        // Act
        await fixture.Host.ReleaseAsync<ObservationPeriodCloseFrozen>(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId).Stream);
        var reopened = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request() with
        {
            PeriodId = reopenedId,
        }), CancellationToken.None);
        await fixture.RunProcessAsync(reopenedId);
        var calendar = await fixture.Reader.HydrateAsync(new TypeIIObservationCalendar(fixture.TenantId,
            fixture.ProgramId));

        // Assert
        Assert.True(blocked.IsSuccess, blocked.Error?.Message);
        Assert.Equal(0, stillBlocked.Revision);
        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        Assert.Equal(TypeIIRangeReservationState.Released, calendar.Find(fixture.PeriodId)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, calendar.Find(reopenedId)!.State);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpActorGivenStartCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedApprovedPlanAsync();
        var context = new Cntryl.Portia.RequestContext<StartTypeIIObservationPeriod>(fixture.Request(),
            new RequestDispatchContext(new ClaimsPrincipal(),
                new HttpInvocation("PUT", "/type-ii-periods", "/type-ii-periods", "start-test")));

        // Act
        var result = await fixture.Handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
        Assert.Null(saved.Pending);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ClaimsPrincipal actor;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid EngagementId => ExaminationId;
        public Uuid BoundaryId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid DescriptionId { get; } = Uuid.CreateVersion4();
        public Uuid DescriptionVersionId { get; } = Uuid.CreateVersion4();
        public Uuid OutcomeId { get; } = Uuid.CreateVersion4();
        public Uuid PlanId { get; } = Uuid.CreateVersion4();
        public Uuid PeriodId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid MemberId => RbacIds.Member(TenantId, UserId);
        public Uuid AuthorId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerId { get; } = Uuid.CreateVersion4();
        public Uuid ExecutiveId { get; } = Uuid.CreateVersion4();
        public Uuid OtherReviewerId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId { get; } = Uuid.CreateVersion4();
        public Uuid ControlVersionId => ControlVersionIds.Initial(ControlId);
        public DateOnly PeriodStart { get; } = new(2026, 10, 1);
        public DateOnly PeriodEnd { get; } = new(2027, 9, 30);
        public MutableTimeProvider Clock { get; } = new(Now);
        public TypeIIRangeHost Host { get; }
        public IAggregateReader Reader => Host.Reader;
        public IAggregateWriter Writer => Host.Writer;
        public StartTypeIIObservationPeriodHandler Handler { get; }
        public IReadOnlyList<TypeIBaselineSourceReference> BaselineSources { get; private set; } = [];

        public Fixture()
        {
            actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                    new Claim("name", "Period manager")], "BdgrzSession"));
            Host = new TypeIIRangeHost(Clock);
            Handler = Host.StartHandler(Clock);
        }

        /// <summary>Starts (if absent) and drives the range process of an admitted opening to its end.</summary>
        public Task<ProcessRecord> RunProcessAsync(Uuid? periodId = null) =>
            Host.RunProcessAsync(TenantId, ProgramId, periodId ?? PeriodId, periodId ?? PeriodId);

        /// <summary>Approves and freezes the close of the started period, as the executive handlers leave it.</summary>
        public async Task FreezeCloseAsync()
        {
            var period = await Reader.HydrateAsync(new TypeIIPeriod(TenantId, ProgramId, PeriodId));
            var sourceReferences = BaselineSources.Select(ToReference).ToArray();
            var expected = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
            var validatedAt = Now.AddMonths(12);
            Assert.Null(period.RecordCloseValidation(period.Revision,
                new TypeIIPeriodCloseValidationContent(sourceReferences, expected, expected, [], [], []), AuthorId,
                "Close validator", validatedAt));
            Assert.Null(period.ApproveClose(period.Revision, [], "Reviewed the complete period.", ExecutiveId, "CEO",
                "Chief Executive", validatedAt.AddHours(1)));
            Assert.Null(period.FreezeClose(period.Revision, new TypeIIPeriodCloseSnapshotContent(
                Uuid.CreateVersion4(), sourceReferences, expected, []), ExecutiveId, "Chief Executive",
                validatedAt.AddHours(2)));
            await Writer.SaveAsync(period, new RequestDispatchContext(RequestActor.System));
        }

        public StartTypeIIObservationPeriod Request() => new(TenantId, ProgramId, PeriodId,
            ExaminationId, PlanId, PeriodStart, PeriodEnd);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(actor, new HttpInvocation("PUT",
                "/api/v1/tenants/tenant/programs/program/type-ii-periods", "/type-ii-periods",
                "start-type-ii-period-test")));

        public ExaminationContentReference ToReference(TypeIBaselineSourceReference source) =>
            new(source.Kind, source.RecordId, source.VersionId, source.Revision, source.ContentSha256);

        public async Task SeedApprovedPlanAsync(Uuid? sourceSnapshotId = null,
            Uuid? controlVersionId = null)
        {
            var ownerMember = new Member(TenantId, UserId);
            Assert.True(ownerMember.Register().IsSuccess);
            await Writer.SaveAsync(ownerMember, new RequestDispatchContext(RequestActor.System));

            var control = new ControlDraft(TenantId, ControlId);
            Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-T2-01",
                new ControlDraftContent("Quarterly access review", "Review user access",
                    "Management reviews access quarterly.",
                    "The security lead reviews the access roster.", ["Dated review record"]),
                AuthorId, "Control author", Now.AddMinutes(-20)).IsSuccess);
            var controlScope = new ResponsibilityScope("control", ControlId, ControlVersionId, 1);
            Assert.Null(control.AssignResponsibility(controlScope, Uuid.CreateVersion4(), MemberId,
                ResponsibilityType.ControlOwner, AuthorId, "Control author", Now.AddMinutes(-18),
                Now.AddMinutes(-19), null, []));
            var controlReviewId = Uuid.CreateVersion4();
            Assert.Null(control.Review(ProgramId, 1, controlReviewId, "accept",
                "The quarterly evidence is defined.", ReviewerId, "Control reviewer",
                Now.AddMinutes(-17)));
            Assert.Null(control.Approve(ProgramId, 1, Uuid.CreateVersion4(), controlReviewId,
                new DateOnly(2026, 10, 1), "Approved for operation.", new HashSet<Uuid> { MemberId },
                ExecutiveId, "Chief Executive", Now.AddMinutes(-16)));
            var controlVersion = control.ApprovedVersion!;
            await Writer.SaveAsync(control, new RequestDispatchContext(RequestActor.System));

            var operatingPlanId = Uuid.CreateVersion4();
            await ControlOperatingSeed.ProposeAsync(Host.Provider, TenantId, ProgramId, ControlId, controlVersion.VersionId,
                controlVersion.Content.ExpectedEvidenceDescriptions, operatingPlanId, new OperatingHolder("member", MemberId), null, ReviewerId,
                new ControlCadence(ControlCadenceSchedule.Recurring, "quarterly", PeriodStart, 5), PeriodStart,
                "Operate quarterly and retain dated evidence.", AuthorId, "Control plan author", Now.AddMinutes(-14));
            await ControlOperatingSeed.ApproveAsync(Host.Provider, TenantId, ProgramId, ControlId, operatingPlanId,
                ExecutiveId, "Chief Executive", Now.AddMinutes(-13));

            var systemDescriptionSource = new SystemDescriptionSourceReference("approved_boundary",
                BoundaryId, Uuid.CreateVersion4(), 1, Hash('a'), "current");
            var description = new SystemDescriptionAggregate(TenantId, ProgramId, DescriptionId);
            var descriptionContent = new SystemDescriptionDraftContent("Sample system", Sections(),
                [systemDescriptionSource], [], []);
            Assert.Null(description.Create(DescriptionVersionId,
                new SystemDescriptionScope("type_i", ExaminationId, BaselineId, null, null),
                descriptionContent, AuthorId, "Description author", Now.AddMinutes(-10)));
            Assert.Null(description.Review(1, ReviewerId, "Independent reviewer", "approve",
                "The system description is complete.", Now.AddMinutes(-9)));
            Assert.Null(description.Approve(1, ExecutiveId, "Chief Executive",
                "Approved the Type I system description.", Now.AddMinutes(-8)));

            var descriptionVersion = description.FindVersion(DescriptionVersionId)!;
            BaselineSources =
            [
                new("approved_boundary", BoundaryId, systemDescriptionSource.VersionId, 1, Hash('a')),
                new("approved_control", ControlId, ControlVersionId, controlVersion.Revision, Hash('c')),
                new("system_description", DescriptionId, DescriptionVersionId, 1,
                    descriptionVersion.ContentSha256),
            ];
            var engagement = new ExaminationEngagementAggregate(TenantId, ProgramId, EngagementId);
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i", "Audit Firm",
                new DateOnly(2026, 9, 30), null, ["security"], BoundaryId, []),
                AuthorId, "Examiner", Now.AddMinutes(-12)));
            var preview = TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId,
                EngagementId, BaselineSources);
            Assert.Null(engagement.FreezeBaseline(1, BaselineId, preview,
                "The frozen baseline includes the approved system description.", AuthorId,
                "Examiner", Now.AddMinutes(-7)));
            Assert.Null(description.BindToExamination(1, ExaminationId, "type_i_baseline", BaselineId,
                ExecutiveId, "Chief Executive", Now.AddMinutes(-6)));
            await Writer.SaveAsync(engagement, new RequestDispatchContext(RequestActor.System));
            await Writer.SaveAsync(description, new RequestDispatchContext(RequestActor.System));

            var baseline = engagement.Baseline!;
            var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
            Assert.Null(outcome.Record(new AuditOutcomeContent(ExaminationId, "type_i", BaselineId,
                baseline.ContentSha256, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), Hash('b'),
                "auditor", "unmodified_opinion", "No material exceptions.", "Report reference",
                null, null, new DateOnly(2026, 10, 1)), OtherReviewerId, "Auditor", Now.AddMinutes(-5)));
            await Writer.SaveAsync(outcome, new RequestDispatchContext(RequestActor.System));

            var planContent = new TypeIIPlanContent(ExaminationId,
                sourceSnapshotId ?? BaselineId, baseline.ContentSha256, DescriptionVersionId,
                PeriodStart, PeriodEnd, "Audit Firm", ["security"], BaselineId, OutcomeId,
                BaselineSources.Select(ToReference).ToArray(),
                [new TypeIIControlPlanEntry(ControlId, controlVersionId ?? ControlVersionId,
                    controlVersion.Content.Title, MemberId, "quarterly", 4, PeriodStart, null, 5,
                    controlVersion.Content.ExpectedEvidenceDescriptions)],
                [], [], []);
            var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
            Assert.Null(plan.Propose(planContent, AuthorId, "Plan author", Now.AddMinutes(-4)));
            Assert.Null(plan.Review(plan.Revision, 1, "approve", "The proposed plan is complete.",
                ReviewerId, "Independent reviewer", Now.AddMinutes(-3)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved for the observation period.",
                ExecutiveId, "CEO", "Chief Executive", Now.AddMinutes(-2)));
            await Writer.SaveAsync(plan, new RequestDispatchContext(RequestActor.System));
        }

        static SystemDescriptionSection[] Sections() => RequiredSections.Select(id =>
            new SystemDescriptionSection(id, id.Replace('_', ' '), "Current system fact.",
                "complete", null)).ToArray();

        static string Hash(char value) => new(value, 64);

        public ValueTask DisposeAsync() => Host.DisposeAsync();
    }

    sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        DateTimeOffset current = current;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }
}
