using System.Security.Claims;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorHandoffCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, "closed")]
    [InlineData(true, "revoked")]
    public async Task ShouldEndOpenHandoffGivenAuthorizedManagementCommand(bool revoke, string status)
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedOpenHandoffAsync();
        var request = revoke
            ? (IRequestBase)new RevokeAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                handoffId, "The review is no longer required")
            : new CloseAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
                handoffId, "The review is complete");

        // Act
        var result = revoke
            ? await new RevokeAdvisorHandoffHandler(fixture.Executor, fixture.Clock)
                .HandleAsync(Fixture.Context(fixture.UserId, (RevokeAdvisorHandoff)request),
                    CancellationToken.None)
            : await new CloseAdvisorHandoffHandler(fixture.Executor, fixture.Clock)
                .HandleAsync(Fixture.Context(fixture.UserId, (CloseAdvisorHandoff)request),
                    CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(status, saved.Status);
        Assert.Equal(3, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenHandoffTermination()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = await fixture.SeedOpenHandoffAsync();
        var request = new CloseAdvisorHandoff(fixture.TenantId, fixture.ProgramId,
            handoffId, "The review is complete");

        // Act
        var result = await new CloseAdvisorHandoffHandler(fixture.Executor, fixture.Clock)
            .HandleAsync(new Cntryl.Portia.RequestContext<CloseAdvisorHandoff>(request,
                RequestActor.System), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AdvisorHandoff(fixture.TenantId,
            fixture.ProgramId, handoffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("ready_for_review", saved.Status);
        Assert.Equal(2, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public TimeProvider Clock { get; } = new FixedTimeProvider(Now.AddMinutes(2));
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public async Task<Uuid> SeedOpenHandoffAsync()
        {
            var handoffId = Uuid.CreateVersion4();
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            Assert.True(handoff.Create("Type I controls", [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())],
                new DateOnly(2026, 12, 31), ActorReference.ForMember(RbacIds.Member(TenantId, UserId),
                    "Compliance lead"), Now).IsSuccess);
            Assert.True(handoff.MarkReadyForReview(1,
                ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Compliance lead"),
                Now.AddMinutes(1)).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return handoffId;
        }

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
                new HttpInvocation("POST", "/api/v1/advisor-handoffs/end",
                    "/api/v1/advisor-handoffs/end", "advisor-handoff-handler-test")));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "lead@example.test")], "BdgrzSession"));

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
