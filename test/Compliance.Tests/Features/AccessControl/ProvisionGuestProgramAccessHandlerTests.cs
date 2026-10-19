using System.Security.Claims;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ProvisionGuestProgramAccessHandlerTests
{
    [Fact]
    public async Task ShouldRejectHumanRequestGivenGuestGrantProvisioningIsAnInternalReaction()
    {
        // Arrange
        var request = new ProvisionGuestProgramAccess(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), new DateOnly(2026, 11, 30),
            new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero),
            Uuid.CreateVersion4(), "Org Admin");
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "test"));

        // Act
        var result = await new GuestInvitationReactionAuthorizer().AuthorizeAsync(
            new RequestContext<ProvisionGuestProgramAccess>(request, actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldIssueIdempotentViewerGrantGivenRegisteredGuestAndAcceptedInvitation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var inviterId = Uuid.CreateVersion4();
        var acceptedAt = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var expiresOn = new DateOnly(2026, 11, 30);
        var now = acceptedAt.AddHours(1);
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var member = new Member(tenantId, userId);
        Assert.True(member.Register("guest", expiresOn).IsSuccess);
        var membershipEpisodeId = member.MembershipEpisodeId;
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var handler = new ProvisionGuestProgramAccessHandler(
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), reader,
            new ExistingProgramScopes(tenantId, programId), new FixedTimeProvider(now));
        var request = new ProvisionGuestProgramAccess(tenantId, userId, programId,
            invitationId, expiresOn, acceptedAt, inviterId, "Org Admin");
        var context = new RequestContext<ProvisionGuestProgramAccess>(request, RequestActor.System);

        // Act
        var first = await handler.HandleAsync(context, CancellationToken.None);
        var second = await handler.HandleAsync(context, CancellationToken.None);
        var grantId = GuestInvitationGrantIds.For(tenantId, RbacIds.Member(tenantId, userId),
            membershipEpisodeId, programId);
        var grant = new List<DomainEvent>();
        await foreach (var item in events.ReadAsync(new AccessGrant(tenantId, grantId).Stream, 0))
            grant.Add(item.Event);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        var issued = Assert.IsType<AccessGrantIssued>(Assert.Single(grant));
        Assert.Equal(BuiltInRbac.ViewerRoleId(tenantId), issued.Terms.RoleId);
        Assert.Equal(new AccessGrantScope(AccessGrantScopeKind.Program, programId), issued.Terms.Scope);
        Assert.Equal(new AccessGrantSource("tenant_invitation", invitationId.ToString()),
            issued.Terms.Source);
        Assert.Equal(RbacIds.Member(tenantId, inviterId).ToString(), issued.Terms.GrantedBy.Id);
        Assert.Equal("Org Admin", issued.Terms.GrantedBy.Display);
        Assert.Equal(acceptedAt, issued.Terms.EffectiveFrom);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
            issued.Terms.EffectiveUntil);
        Assert.Equal(membershipEpisodeId, issued.MembershipEpisodeId);
    }

    [Fact]
    public async Task ShouldRejectGuestGrantGivenProgramOutsideTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var acceptedAt = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var expiresOn = new DateOnly(2026, 11, 30);
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var member = new Member(tenantId, userId);
        Assert.True(member.Register("guest", expiresOn).IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(member,
            new RequestDispatchContext(RequestActor.System));
        var handler = new ProvisionGuestProgramAccessHandler(
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            new ExistingProgramScopes(tenantId, Uuid.Empty), new FixedTimeProvider(acceptedAt));
        var request = new ProvisionGuestProgramAccess(tenantId, userId, programId,
            Uuid.CreateVersion4(), expiresOn, acceptedAt,
            Uuid.CreateVersion4(), "Org Admin");

        // Act
        var result = await handler.HandleAsync(new RequestContext<ProvisionGuestProgramAccess>(
            request, RequestActor.System), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    sealed class ExistingProgramScopes(Uuid tenantId, Uuid programId)
        : IProgramResourceScopeResolver
    {
        public ValueTask<bool> IsTenantProgramAsync(Uuid requestedTenantId, Uuid requestedProgramId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == requestedTenantId && programId == requestedProgramId);

        public ValueTask<bool> IsTenantApplicationAsync(Uuid requestedTenantId, Uuid applicationId,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<Uuid?> ResolveProgramIdAsync(Uuid requestedTenantId,
            IProgramResourceRequest request, CancellationToken ct = default) =>
            ValueTask.FromResult<Uuid?>(null);
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
