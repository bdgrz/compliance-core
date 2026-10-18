using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class TypeIIPlanCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordIndependentReviewGivenProposedPlan()
    {
        // Arrange
        await using var fixture = new Fixture();
        var planId = await fixture.SeedPlanAsync(fixture.AuthorUserId);

        // Act
        var result = await fixture.ReviewHandler().HandleAsync(Fixture.Context(
            fixture.ReviewerUserId, new ReviewTypeIIOperatingPlan(fixture.TenantId,
                fixture.ProgramId, planId, 1, "approve", "All control owners and gaps were reviewed.")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, planId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("reviewed", saved.Current!.Status);
        var review = Assert.Single(saved.Current.Reviews);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.ReviewerUserId),
            review.ReviewerMemberId);
        Assert.Equal("approve", review.Outcome);
    }

    [Fact]
    public async Task ShouldRejectPlanProposerReviewGivenProposedPlan()
    {
        // Arrange
        await using var fixture = new Fixture();
        var planId = await fixture.SeedPlanAsync(fixture.ReviewerUserId);

        // Act
        var result = await fixture.ReviewHandler().HandleAsync(Fixture.Context(
            fixture.ReviewerUserId, new ReviewTypeIIOperatingPlan(fixture.TenantId,
                fixture.ProgramId, planId, 1, "approve", "Review attempted by its proposer.")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, planId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(saved.Current!.Reviews);
        Assert.Equal(1, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now);
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid AuthorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public ReviewTypeIIOperatingPlanHandler ReviewHandler() =>
            new(Executor, _clock);

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor(userId),
            new HttpInvocation("POST", "/api/v1/type-ii-plans/review",
                "/api/v1/type-ii-plans/review", "type-ii-plan-test")));

        public async Task<Uuid> SeedPlanAsync(Uuid proposerUserId)
        {
            var planId = Uuid.CreateVersion4();
            var plan = new TypeIIPlanAggregate(TenantId, ProgramId, planId);
            Assert.Null(plan.Propose(Content(ExaminationId),
                RbacIds.Member(TenantId, proposerUserId), "Plan proposer", Now));
            await Writer.SaveAsync(plan, new RequestDispatchContext(RequestActor.System));
            return planId;
        }

        static TypeIIPlanContent Content(Uuid examinationId)
        {
            var gapId = Uuid.CreateVersion4();
            return new TypeIIPlanContent(examinationId, Uuid.CreateVersion4(),
                new string('a', 64), Uuid.CreateVersion4(), new DateOnly(2026, 10, 1),
                new DateOnly(2027, 9, 30), "Audit Firm", ["security", "availability"],
                Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                [Reference("policy"), Reference("risk"), Reference("vendor"),
                    Reference("finding"), Reference("change")],
                [new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Quarterly access review",
                    Uuid.CreateVersion4(), "quarterly", 4, new DateOnly(2026, 10, 1), null)],
                [new("risk", gapId, Uuid.CreateVersion4(), 1, new string('b', 64), "carry")],
                [new(gapId, "Known opening gap", "acknowledged", "Owner and due date recorded")],
                [new(Uuid.CreateVersion4(), "Enable the new access review control",
                    new DateOnly(2026, 12, 1))]);
        }

        static ExaminationContentReference Reference(string sourceType) =>
            new(sourceType, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
                new string('c', 64));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "plan-review@example.test")], "BdgrzSession"));

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
