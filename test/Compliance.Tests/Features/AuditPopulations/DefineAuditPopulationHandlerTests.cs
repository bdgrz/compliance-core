using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Handlers;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceFacts;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPopulationAggregate = Bdgrz.Compliance.Features.AuditPopulations.Aggregates.AuditPopulation;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.TypeIIPlan;
using TypeIIPeriodAggregate = Bdgrz.Compliance.Features.Operations.TypeIIPeriodRules;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

public sealed partial class DefineAuditPopulationHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);
    static readonly DateOnly PeriodStart = new(2026, 10, 1);
    static readonly DateOnly PeriodEnd = new(2027, 9, 30);

    [Fact]
    public async Task ShouldDefinePopulationFromExactFrozenCloseGivenCompleteDefinitionAndPersonalActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var closeSnapshotId = await fixture.SeedClosedPeriodAsync();
        var definition = fixture.Definition();
        var request = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId,
            fixture.PopulationId, fixture.PeriodId, closeSnapshotId, definition);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId)).Rules;

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("defined", saved.Current!.Status);
        Assert.Equal(fixture.PeriodId, saved.Current.TypeIIPeriodId);
        Assert.Equal(fixture.ExaminationId, saved.Current.ExaminationId);
        Assert.Equal(closeSnapshotId, saved.Current.CloseSnapshot.CloseSnapshotId);
        Assert.Equal(definition.Name, saved.Current.CurrentDefinition!.Content.Name);
        Assert.Equal(definition.Purpose, saved.Current.CurrentDefinition.Content.Purpose);
        Assert.Equal(definition.SourceUniverse, saved.Current.CurrentDefinition.Content.SourceUniverse);
        Assert.Equal(definition.InclusionRules, saved.Current.CurrentDefinition.Content.InclusionRules);
        Assert.Equal(definition.ExclusionRules, saved.Current.CurrentDefinition.Content.ExclusionRules);
        Assert.Equal(definition.ExpectedCompletenessEvidence,
            saved.Current.CurrentDefinition.Content.ExpectedCompletenessEvidence);
        Assert.Equal(definition.AuthorizedFields.Order(StringComparer.Ordinal),
            saved.Current.CurrentDefinition.Content.AuthorizedFields);
        Assert.Equal(definition.ControlIds, saved.Current.CurrentDefinition.Content.ControlIds);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId),
            saved.Current.CurrentDefinition.RecordedByMemberId);
    }

    [Fact]
    public async Task ShouldRejectNonCurrentCloseSnapshotGivenPopulationDefinition()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedClosedPeriodAsync();
        var request = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId,
            fixture.PopulationId, fixture.PeriodId, Uuid.CreateVersion4(), fixture.Definition());

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId)).Rules;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpActorGivenPopulationDefinition()
    {
        // Arrange
        await using var fixture = new Fixture();
        var closeSnapshotId = await fixture.SeedClosedPeriodAsync();
        var request = new DefineAuditPopulation(fixture.TenantId, fixture.ProgramId,
            fixture.PopulationId, fixture.PeriodId, closeSnapshotId, fixture.Definition());

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request, RequestActor.System),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly FixedTimeProvider _clock = new(Now.AddMonths(12).AddHours(3));
        public TimeProvider Clock => _clock;
        public void AdvanceClock() => _clock.Now = _clock.Now.AddMinutes(1);
        readonly ClaimsPrincipal _actor;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid PopulationId { get; } = Uuid.CreateVersion4();
        public Uuid PeriodId { get; } = Uuid.CreateVersion4();
        public Uuid PlanId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId { get; } = Uuid.CreateVersion4();
        public Uuid ControlOwnerId { get; } = Uuid.CreateVersion4();
        public Uuid ValidatorId { get; } = Uuid.CreateVersion4();
        public Uuid ExecutiveId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerId { get; } = Uuid.CreateVersion4();
        public Uuid ProposerId { get; } = Uuid.CreateVersion4();
        public Uuid GapId { get; } = Uuid.CreateVersion4();
        public InMemoryEventStore PeriodEvents { get; } = new();
        public IImmutableContentStore PeriodContent { get; } = TypeIIPeriodSeeds.NewContentStore();
        public TypeIIPeriodReader Periods => new(Reader, PeriodEvents, PeriodContent);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            _actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                    new Claim("email", "compliance-lead@example.test")], "BdgrzSession"));
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(PeriodEvents);
            services.AddSingleton<IDomainEventReader>(PeriodEvents);
            services.AddSingleton<IImmutableContentStore>(PeriodContent);
            services.AddSingleton<ISourceFactResolver>(provider => new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
            services.AddPortia();
            services.AddVerifiedAuditPopulationReader();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public AuditPopulationReader Populations => new(Reader, PeriodEvents, PeriodContent, new EventStoreSourceFactResolver(PeriodEvents));
        public AuditPopulationMutations Mutations => new(new NativeOperationExecutor(Reader, Executor, PeriodContent), Reader,
            Executor, PeriodEvents, PeriodContent, new EventStoreSourceFactResolver(PeriodEvents), Periods,
            new AuditPopulationSources(PeriodEvents, new EventStoreSourceFactResolver(PeriodEvents), PeriodContent), Clock);
        public AuditPopulationMutations MutationsFor(IImmutableContentStore content)
        {
            var resolver = new EventStoreSourceFactResolver(PeriodEvents);
            var verified = new AuditPopulationAggregateReader(Reader, PeriodEvents, content, resolver);
            return new(new NativeOperationExecutor(verified, Executor, content), verified, Executor, PeriodEvents,
                content, resolver, new TypeIIPeriodReader(verified, PeriodEvents, content),
                new AuditPopulationSources(PeriodEvents, resolver, content), Clock);
        }
        public DefineAuditPopulationHandler Handler() => new(Mutations, Periods, _clock);

        public Cntryl.Portia.RequestContext<DefineAuditPopulation> Context(
            DefineAuditPopulation request, ClaimsPrincipal? actor = null) => new(request,
            new RequestDispatchContext(actor ?? _actor, new HttpInvocation("POST",
                "/audit-populations/define", "/audit-populations/define", "population-define-test")));

        public async Task<Uuid> SeedClosedPeriodAsync()
        {
            var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
            Assert.Null(plan.Propose(PlanContent(), ProposerId, "Compliance lead", Now.AddDays(-3)));
            Assert.Null(plan.Review(plan.Revision, 1, "approve", "Reviewed controls and opening gaps.",
                ReviewerId, "Independent reviewer", Now.AddDays(-2)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved for observation.", ExecutiveId,
                "CISO", "Executive approver", Now.AddDays(-1)));
            await Writer.SaveAsync(plan, new RequestDispatchContext(RequestActor.System));

            var period = new TypeIIPeriodAggregate(TenantId, ProgramId, PeriodId);
            Assert.Null(period.Start(plan.Current!, Opening(), ProposerId, "Compliance lead", Now));
            var occurrences = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
            Assert.Null(period.RecordCloseValidation(period.Revision,
                new TypeIIPeriodCloseValidationContent(SourceReferences(), occurrences, occurrences,
                    [], [], []), ValidatorId, "Close validator", Now.AddMonths(12)));
            Assert.Null(period.ApproveClose(period.Revision, [], "Reviewed the reconciled close.",
                ExecutiveId, "CEO", "Chief Executive", Now.AddMonths(12).AddHours(1)));
            var closeSnapshotId = Uuid.CreateVersion4();
            Assert.Null(period.FreezeClose(period.Revision,
                new TypeIIPeriodCloseSnapshotContent(closeSnapshotId, SourceReferences(), occurrences, []),
                ExecutiveId, "CEO", Now.AddMonths(12).AddHours(2)));
            await TypeIIPeriodSeeds.WriteAsync(Writer, PeriodContent, period);
            return closeSnapshotId;
        }

        public AuditPopulationDefinitionContent Definition() => new("Production changes",
            "Changes affecting the in-scope service.", "Approved production change register",
            PeriodStart, PeriodEnd, "Include approved production changes.",
            "Exclude canceled and rejected changes.", "Reconcile every source row exactly once.",
            ["control_id", "change_reference"], [ControlId]);

        TypeIIPlanContent PlanContent() => new(ExaminationId, Uuid.CreateVersion4(), Hash('a'),
            Uuid.CreateVersion4(), PeriodStart, PeriodEnd, "Audit Firm", ["security", "availability"],
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), SourceReferences(),
            [new TypeIIControlPlanEntry(ControlId, Uuid.CreateVersion4(), "Quarterly access review",
                ControlOwnerId, "quarterly", 4, PeriodStart, null,
                ExpectedEvidenceDescriptions: ["Dated review record"])],
            [new TypeIICarriedForwardGap("finding", GapId, Uuid.CreateVersion4(), 1, Hash('b'), "carry")],
            [new TypeIIPlanGapAcknowledgement(GapId, "Open gap", "acknowledged",
                "The owner and due date were confirmed.")], []);

        TypeIIPeriodOpeningContent Opening() => new(ExaminationId, Uuid.CreateVersion4(), Hash('c'),
            SourceReferences(), [GapId]);

        static IReadOnlyList<ExaminationContentReference> SourceReferences() =>
            [Reference("boundary"), Reference("inventory"), Reference("control"),
                Reference("policy"), Reference("provider"), Reference("finding")];

        static ExaminationContentReference Reference(string sourceType) => new(sourceType,
            Uuid.CreateVersion5(Uuid.Empty, "population-source-record:" + sourceType),
            Uuid.CreateVersion5(Uuid.Empty, "population-source-version:" + sourceType), 1, Hash('d'));

        static string Hash(char value) => new(value, 64);

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
