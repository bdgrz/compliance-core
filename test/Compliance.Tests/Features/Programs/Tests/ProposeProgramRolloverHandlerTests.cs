using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Tests.Testing;
using AuditOutcomeAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.AuditOutcome;
using ProgramRolloverAggregate = Bdgrz.Compliance.Features.Programs.Aggregates.ProgramRollover;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.Artifacts.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Handlers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProposeProgramRolloverHandlerTests
{
    static readonly DateTimeOffset Now = new(2027, 1, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProposeExactCarryForwardGivenClosedPeriodAndRecordedAuditorOutcome()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(fixture.Content(sources));

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, request.RolloverId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var rollover = saved.Current!;
        Assert.Equal("proposed", rollover.Status);
        Assert.Equal(sources.Period.PeriodId, rollover.Content.SourcePeriodId);
        Assert.Equal(sources.Period.CloseSnapshots[^1].CloseSnapshotId,
            rollover.Content.SourceCloseSnapshotId);
        Assert.Equal(sources.Outcome!.AuditOutcomeId, rollover.Content.AuditOutcomeId);
        Assert.Equal("Carry forward the reviewed control to the next period.",
            rollover.Content.ProposalRationale);
        Assert.Equal(sources.ControlReference.RecordId,
            Assert.Single(rollover.Content.Records).SourceRecordId);
    }

    [Fact]
    public async Task ShouldRejectOpenPeriodGivenRolloverProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync(closePeriod: false);
        var request = fixture.Request(fixture.Content(sources));

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, request.RolloverId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectChangedOutcomeDigestGivenClosedPeriod()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var content = fixture.Content(sources) with { AuditOutcomeContentSha256 = Hash('f') };
        var request = fixture.Request(content);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, request.RolloverId));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldTreatSameProposalRetryAsIdempotentGivenLaterHandlerTime()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(fixture.Content(sources));
        var context = fixture.Context(request);
        var first = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        fixture.Clock.Set(Now.AddHours(2));
        var retry = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ProgramRolloverAggregate(
            fixture.TenantId, fixture.ProgramId, request.RolloverId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(Now, saved.Current!.CurrentVersion.ProposedAt);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenRolloverProposal()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(fixture.Content(sources));

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.SystemContext(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        static readonly DateOnly PeriodStart = new(2026, 1, 1);
        static readonly DateOnly PeriodEnd = new(2026, 12, 31);
        static readonly DateTimeOffset SetupAt = new(2025, 12, 31, 9, 0, 0, TimeSpan.Zero);
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid PeriodId { get; } = Uuid.CreateVersion4();
        public Uuid PlanId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid ActorMemberId => RbacIds.Member(TenantId, UserId);
        public Uuid GapId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId { get; } = Uuid.CreateVersion4();
        public Uuid ControlVersionId { get; } = Uuid.CreateVersion4();
        public MutableTimeProvider Clock { get; } = new(Now);
        public InMemoryEventStore PeriodEvents { get; } = new();
        public IImmutableContentStore PeriodContent { get; } = TypeIIPeriodSeeds.NewContentStore();
        public TypeIIPeriodReader Periods => new(Reader, PeriodEvents, PeriodContent);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(PeriodEvents);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public ProposeProgramRolloverHandler Handler() => new(Executor, Reader, Periods, Clock);

        public async Task<SourceSet> SeedSourcesAsync(bool closePeriod = true)
        {
            var proposalMemberId = Uuid.CreateVersion4();
            var reviewerMemberId = Uuid.CreateVersion4();
            var ceoMemberId = Uuid.CreateVersion4();
            var validatorMemberId = Uuid.CreateVersion4();
            var controlReference = new ExaminationContentReference("control", ControlId,
                ControlVersionId, 3, Hash('d'));
            var sourceReferences = new[]
            {
                Reference("boundary"), Reference("inventory"), controlReference,
                Reference("policy"), Reference("provider"), Reference("finding"),
            };
            var plan = new TypeIIPlan(TenantId, ProgramId, PlanId);
            Assert.Null(plan.Propose(PlanContent(sourceReferences), proposalMemberId,
                "Compliance lead", SetupAt));
            Assert.Null(plan.Review(plan.Revision, 1, "approve",
                "Reviewed the source population and operating cadence.", reviewerMemberId,
                "Independent reviewer", SetupAt.AddHours(1)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved for observation.",
                ceoMemberId, "CISO", "Chief Information Security Officer", SetupAt.AddHours(2)));
            await Writer.SaveAsync(plan, new RequestDispatchContext(RequestActor.System));

            var period = new TypeIIPeriodRules(TenantId, ProgramId, PeriodId);
            Assert.Null(period.Start(plan.Current!, Opening(sourceReferences), proposalMemberId,
                "Compliance lead", SetupAt.AddHours(3)));
            if (closePeriod)
            {
                var expectedOccurrences = period.Current!.ExpectedOccurrences
                    .Select(item => item.OccurrenceId)
                    .OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
                var closeAt = Now.AddDays(-4);
                Assert.Null(period.RecordCloseValidation(period.Revision,
                    new TypeIIPeriodCloseValidationContent(sourceReferences, expectedOccurrences,
                        expectedOccurrences, [], [], [GapId]), validatorMemberId, "Close validator",
                    closeAt));
                Assert.Null(period.ApproveClose(period.Revision, [GapId],
                    "Acknowledged the remaining material gap.", ceoMemberId, "CEO",
                    "Chief Executive", closeAt.AddHours(1)));
                Assert.Null(period.FreezeClose(period.Revision,
                    new TypeIIPeriodCloseSnapshotContent(Uuid.CreateVersion4(), sourceReferences,
                        expectedOccurrences, [GapId]), ceoMemberId, "CEO", closeAt.AddHours(2)));
            }
            await TypeIIPeriodSeeds.WriteAsync(Writer, PeriodContent, period);

            AuditOutcomeView? outcomeView = null;
            if (closePeriod)
            {
                var snapshot = period.Current!.CloseSnapshots[^1];
                var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, Uuid.CreateVersion4());
                var outcomeContent = new AuditOutcomeContent(ExaminationId, "type_ii",
                    snapshot.CloseSnapshotId, snapshot.ContentSha256, Uuid.CreateVersion4(), 1,
                    Uuid.CreateVersion4(), Hash('e'), "auditor", "unmodified",
                    "No material modifications were reported.", "auditor-report-2027-01",
                    null, null, new DateOnly(2027, 1, 4));
                Assert.Null(outcome.Record(outcomeContent, ceoMemberId, "External auditor",
                    Now.AddDays(-1)));
                await Writer.SaveAsync(outcome, new RequestDispatchContext(RequestActor.System));
                outcomeView = outcome.Current;
            }

            return new SourceSet(period.Current!, outcomeView, controlReference);
        }

        public ProgramRolloverContent Content(SourceSet sources)
        {
            var snapshot = sources.Period.CloseSnapshots.Count == 0
                ? null : sources.Period.CloseSnapshots[^1];
            return new ProgramRolloverContent(sources.Period.PeriodId, ExaminationId,
                snapshot?.CloseSnapshotId ?? Uuid.CreateVersion4(),
                snapshot?.ContentSha256 ?? Hash('a'),
                sources.Outcome?.AuditOutcomeId ?? Uuid.CreateVersion4(),
                sources.Outcome?.ContentSha256 ?? Hash('b'),
                new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31),
                Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                [new ProgramRolloverRecordContent("control", ControlId, 3, Hash('d'),
                    "carried", Uuid.CreateVersion4(), "The control remains in scope.")], [],
                ["readiness", "open_findings"])
            {
                ProposalRationale = "Carry forward the reviewed control to the next period.",
            };
        }

        public ProposeProgramRollover Request(ProgramRolloverContent content) =>
            new(TenantId, ProgramId, Uuid.CreateVersion4(), content);

        public Cntryl.Portia.RequestContext<ProposeProgramRollover> Context(
            ProposeProgramRollover request)
        {
            var actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                    new Claim("email", "compliance-lead@example.test")], "BdgrzSession"));
            return new Cntryl.Portia.RequestContext<ProposeProgramRollover>(request,
                new RequestDispatchContext(actor, new HttpInvocation("POST",
                    "/api/v1/tenants/test/programs/test/rollovers/proposals",
                    "/api/v1/tenants/test/programs/test/rollovers/proposals",
                    "propose-program-rollover-test")));
        }

        public static Cntryl.Portia.RequestContext<ProposeProgramRollover> SystemContext(
            ProposeProgramRollover request) => new(request,
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("POST",
                "/api/v1/tenants/test/programs/test/rollovers/proposals",
                "/api/v1/tenants/test/programs/test/rollovers/proposals",
                "propose-program-rollover-test")));

        TypeIIPlanContent PlanContent(IReadOnlyList<ExaminationContentReference> refs) =>
            new(ExaminationId, Uuid.CreateVersion4(), Hash('a'), Uuid.CreateVersion4(),
                PeriodStart, PeriodEnd, "Independent Audit LLP", ["security", "availability"],
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), refs,
                [new TypeIIControlPlanEntry(ControlId, Uuid.CreateVersion4(),
                    "Quarterly access review", Uuid.CreateVersion4(), "quarterly", 4,
                    PeriodStart, null, ExpectedEvidenceDescriptions: ["Dated review record"])],
                [new TypeIICarriedForwardGap("finding", GapId, Uuid.CreateVersion4(), 1,
                    Hash('c'), "carry")],
                [new TypeIIPlanGapAcknowledgement(GapId, "Open control gap", "acknowledged",
                    "The control owner and due date were confirmed.")], []);

        TypeIIPeriodOpeningContent Opening(IReadOnlyList<ExaminationContentReference> refs) =>
            new(ExaminationId, Uuid.CreateVersion4(), Hash('a'), refs, [GapId]);

        static ExaminationContentReference Reference(string type) =>
            new(type, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Hash('c'));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed record SourceSet(TypeIIPeriodView Period, AuditOutcomeView? Outcome,
        ExaminationContentReference ControlReference);

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Set(DateTimeOffset value) => _utcNow = value;
    }

    static string Hash(char value) => new(value, 64);
}
