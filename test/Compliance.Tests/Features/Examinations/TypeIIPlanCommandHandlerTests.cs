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

    [Fact]
    public async Task ShouldApproveReviewedPlanGivenCurrentCisoPermission()
    {
        // Arrange
        await using var fixture = new Fixture();
        var planId = await fixture.SeedPlanAsync(fixture.AuthorUserId);
        Assert.True((await fixture.ReviewHandler().HandleAsync(Fixture.Context(
            fixture.ReviewerUserId, new ReviewTypeIIOperatingPlan(fixture.TenantId,
                fixture.ProgramId, planId, 1, "approve", "Reviewed the opening plan.")),
            CancellationToken.None)).IsSuccess);
        await fixture.RegisterMemberAsync(fixture.ApproverUserId);
        var request = new ApproveTypeIIOperatingPlan(fixture.TenantId, fixture.ProgramId,
            planId, 1, "The opening controls and acknowledged gaps are approved.");

        // Act
        var result = await fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso)
            .HandleAsync(Fixture.Context(fixture.ApproverUserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, planId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("approved", saved.Current!.Status);
        var approval = Assert.Single(saved.Current.Approvals);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.ApproverUserId),
            approval.ApprovedByMemberId);
        Assert.Equal("ciso", approval.ApproverRole);
    }

    [Fact]
    public async Task ShouldRejectClaimedCeoRoleGivenNoExecutivePermission()
    {
        // Arrange
        await using var fixture = new Fixture();
        var planId = await fixture.SeedPlanAsync(fixture.AuthorUserId);
        Assert.True((await fixture.ReviewHandler().HandleAsync(Fixture.Context(
            fixture.ReviewerUserId, new ReviewTypeIIOperatingPlan(fixture.TenantId,
                fixture.ProgramId, planId, 1, "approve", "Reviewed the opening plan.")),
            CancellationToken.None)).IsSuccess);
        await fixture.RegisterMemberAsync(fixture.ApproverUserId);
        var request = new ApproveTypeIIOperatingPlan(fixture.TenantId, fixture.ProgramId,
            planId, 1, "Token claims do not establish executive authority.");

        // Act
        var result = await fixture.ApproveHandler().HandleAsync(Fixture.Context(
            fixture.ApproverUserId, request, roleClaim: "ceo"), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, planId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.Approvals);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldTreatApprovalRetryAsIdempotentGivenSameIntentAndLaterClockTime()
    {
        // Arrange
        await using var fixture = new Fixture();
        var planId = await fixture.SeedPlanAsync(fixture.AuthorUserId);
        Assert.True((await fixture.ReviewHandler().HandleAsync(Fixture.Context(
            fixture.ReviewerUserId, new ReviewTypeIIOperatingPlan(fixture.TenantId,
                fixture.ProgramId, planId, 1, "approve", "Reviewed the opening plan.")),
            CancellationToken.None)).IsSuccess);
        await fixture.RegisterMemberAsync(fixture.ApproverUserId);
        var handler = fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso);
        var request = new ApproveTypeIIOperatingPlan(fixture.TenantId, fixture.ProgramId,
            planId, 1, "The opening controls and acknowledged gaps are approved.");
        var context = Fixture.Context(fixture.ApproverUserId, request);
        Assert.True((await handler.HandleAsync(context, CancellationToken.None)).IsSuccess);
        fixture.Clock.Set(Now.AddDays(3));

        // Act
        var retry = await handler.HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new TypeIIPlanAggregate(fixture.TenantId,
            fixture.ProgramId, planId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(3, saved.Revision);
        Assert.Single(saved.Current!.Approvals);
        Assert.Equal(Now, saved.Current.Approvals[0].ApprovedAt);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly MutableTimeProvider _clock = new(Now);
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid AuthorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public MutableTimeProvider Clock => _clock;

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

        public ApproveTypeIIOperatingPlanHandler ApproveHandler(params string[] permissions) =>
            new(Executor, new ManagementExecutiveAuthority(Reader,
                new ExecutivePermissions(permissions)), _clock);

        public async Task RegisterMemberAsync(Uuid userId)
        {
            var member = new Member(TenantId, userId);
            Assert.True(member.Register().IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request,
            string? roleClaim = null)
            where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor(userId, roleClaim),
                new HttpInvocation("POST", request is ApproveTypeIIOperatingPlan
                        ? "/api/v1/type-ii-plans/approve"
                        : "/api/v1/type-ii-plans/review",
                    request is ApproveTypeIIOperatingPlan
                        ? "/api/v1/type-ii-plans/approve"
                        : "/api/v1/type-ii-plans/review",
                    request is ApproveTypeIIOperatingPlan
                        ? "approve-type-ii-plan-test"
                        : "type-ii-plan-test")));

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

        static ClaimsPrincipal Actor(Uuid userId, string? roleClaim = null)
        {
            var claims = new List<Claim>
            {
                new("iss", "bdgrz"),
                new("sub", userId.ToString()),
                new("email", "plan-review@example.test"),
            };
            if (roleClaim is not null)
                claims.Add(new Claim(ClaimTypes.Role, roleClaim));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "BdgrzSession"));
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _utcNow = utcNow;

        public void Set(DateTimeOffset value) => _utcNow = value;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
