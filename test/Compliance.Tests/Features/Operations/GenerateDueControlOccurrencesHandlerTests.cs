using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class GenerateDueControlOccurrencesHandlerTests
{
    static readonly DateTimeOffset StartedAt = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset GeneratedAt = StartedAt.AddDays(1);

    [Fact]
    public async Task ShouldRecordExactDuePeriodOccurrencesAndRetryOnceGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var request = new GenerateDueControlOccurrences(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, new DateOnly(2027, 3, 1), "period-generation-001");

        // Act
        var first = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var retry = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(2, saved.Revision);
        var batch = Assert.Single(saved.OccurrenceGenerationBatches);
        var current = Assert.IsType<TypeIIPeriodView>(saved.Current);
        Assert.Equal("period-generation-001", batch.GenerationKey);
        Assert.Equal(request.ThroughDate, batch.ThroughDate);
        Assert.Equal(current.ExpectedOccurrences.Where(item => item.DueOn <= request.ThroughDate)
                .Select(item => item.OccurrenceId).OrderBy(static id => id.ToString()),
            batch.OccurrenceIds);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.ActorUserId), batch.ActorMemberId);
        Assert.Equal(GeneratedAt, batch.GeneratedAt);
    }

    [Fact]
    public async Task ShouldRejectGenerationKeyReuseWithDifferentThroughDateGivenPriorBatch()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var request = new GenerateDueControlOccurrences(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, new DateOnly(2027, 3, 1), "period-generation-001");
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None)).IsSuccess);
        var changed = request with { ThroughDate = new DateOnly(2027, 4, 1) };

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(changed), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(2, saved.Revision);
        Assert.Single(saved.OccurrenceGenerationBatches);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenOccurrenceGenerationCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var request = new GenerateDueControlOccurrences(fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, new DateOnly(2027, 3, 1), "period-generation-001");

        // Act
        var result = await fixture.Handler.HandleAsync(new RequestContext<GenerateDueControlOccurrences>(
            request, RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId,
            fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
        Assert.Empty(saved.OccurrenceGenerationBatches);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
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
        public Uuid ControlOwnerId { get; } = Uuid.CreateVersion4();
        public Uuid GapId { get; } = Uuid.CreateVersion4();
        public IAggregateExecutor Executor { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateReader Reader { get; }
        public GenerateDueControlOccurrencesHandler Handler { get; }
        public TimeProvider Clock { get; } = new FixedTimeProvider(GeneratedAt);

        public Fixture()
        {
            _actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", ActorUserId.ToString()),
                    new Claim("name", "Period manager")], "BdgrzSession"));
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Handler = new GenerateDueControlOccurrencesHandler(Executor, Clock);
        }

        public Cntryl.Portia.RequestContext<GenerateDueControlOccurrences> Context(
            GenerateDueControlOccurrences request) => new(request,
            new RequestDispatchContext(_actor, new HttpInvocation("POST",
                "/type-ii-periods/generate-occurrences", "/type-ii-periods/generate-occurrences",
                "period-occurrence-generation-test")));

        public async Task SeedStartedPeriodAsync()
        {
            var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
            Assert.Null(plan.Propose(PlanContent(), ProposerMemberId, "Plan proposer", StartedAt.AddDays(-3)));
            Assert.Null(plan.Review(plan.Revision, 1, "approve", "Exact controls and opening gaps reviewed.",
                ReviewerMemberId, "Independent reviewer", StartedAt.AddDays(-2)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved for observation.", ApproverMemberId,
                "CISO", "Executive approver", StartedAt.AddDays(-1)));
            await Writer.SaveAsync(plan, new RequestDispatchContext(RequestActor.System));

            var period = new TypeIIPeriod(TenantId, ProgramId, PeriodId);
            Assert.Null(period.Start(plan.Current!, Opening(), ProposerMemberId, "Compliance lead", StartedAt));
            await Writer.SaveAsync(period, new RequestDispatchContext(RequestActor.System));
        }

        TypeIIPlanContent PlanContent() => new(ExaminationId, Uuid.CreateVersion4(), new string('a', 64),
            Uuid.CreateVersion4(), new DateOnly(2026, 10, 1), new DateOnly(2027, 9, 30),
            "Audit Firm", ["security", "availability"], Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            [Reference("policy"), Reference("risk"), Reference("vendor"), Reference("finding"),
                Reference("change")],
            [new TypeIIControlPlanEntry(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                "Quarterly access review", ControlOwnerId, "quarterly", 4, new DateOnly(2026, 10, 1), null,
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

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
