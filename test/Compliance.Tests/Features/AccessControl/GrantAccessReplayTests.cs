using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class GrantAccessReplayTests
{
    [Fact]
    public async Task ShouldReplayWithoutReselectingRetiredMemberOrRoleGivenOriginalAcceptedGrant()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request();
        var handler = fixture.Handler();
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        fixture.Members.Available = false;
        fixture.Validator.Available = false;

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var grant = await fixture.Reader.HydrateAsync(new AccessGrant(request.TenantId, request.GrantId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1UL, grant.CommittedStreamPosition);
        Assert.False(grant.IsRevoked);
    }

    [Fact]
    public async Task ShouldKeepRevocationAndOriginalEpisodeGivenRetryAfterAuthorityRetirement()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.Request();
        var handler = fixture.Handler();
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var grant = await fixture.Reader.HydrateAsync(new AccessGrant(request.TenantId, request.GrantId));
        Assert.True(grant.Revoke(ActorReference.ForMember(Uuid.CreateVersion4(), "Administrator"), DateTimeOffset.UtcNow).IsSuccess);
        await fixture.Writer.SaveAsync(grant, fixture.Context(request));
        fixture.Members.Available = false;

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var current = await fixture.Reader.HydrateAsync(new AccessGrant(request.TenantId, request.GrantId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(current.IsRevoked);
        Assert.Equal(fixture.Members.Episode, current.MembershipEpisodeId);
        Assert.Equal(2UL, current.CommittedStreamPosition);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly Uuid _tenant = Uuid.CreateVersion4();
        readonly ClaimsPrincipal _actor = new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
        public MemberEligibility Members { get; } = new();
        public ProposalValidator Validator { get; } = new();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton<IMemberAccessEligibility>(Members);
            services.AddSingleton<IAccessGrantProposalValidator>(Validator);
            services.AddPortia();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }
        public GrantAccessHandler Handler() => ActivatorUtilities.CreateInstance<GrantAccessHandler>(_scope.ServiceProvider);
        public Cntryl.Portia.RequestContext<GrantAccess> Context(GrantAccess request) => new(request, _actor);
        public GrantAccess Request() => new(_tenant, Uuid.CreateVersion4(), new(
            new(AccessGrantPrincipalKind.Member, Uuid.CreateVersion4()), Uuid.CreateVersion4(),
            new(AccessGrantScopeKind.Program, Uuid.CreateVersion4()), new("manual", "review"),
            DateTimeOffset.UtcNow.AddDays(-1), null));
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
    sealed class MemberEligibility : IMemberAccessEligibility
    {
        public bool Available { get; set; } = true;
        public Uuid Episode { get; } = Uuid.CreateVersion4();
        public ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(Available);
        public ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid memberId, CancellationToken ct = default) =>
            ValueTask.FromResult<Uuid?>(Available ? Episode : null);
    }
    sealed class ProposalValidator : IAccessGrantProposalValidator
    {
        public bool Available { get; set; } = true;
        public ValueTask<Result> ValidateAsync(Uuid tenantId, AccessGrantProposal proposal, CancellationToken ct = default) =>
            ValueTask.FromResult(Available ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Validation, "Current role unavailable.")));
    }
}
