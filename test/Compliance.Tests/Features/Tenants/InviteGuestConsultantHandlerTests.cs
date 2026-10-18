using System.Security.Claims;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class InviteGuestConsultantHandlerTests
{
    [Fact]
    public async Task ShouldIssueScopedGuestInvitationGivenTenantAdminSelectsCurrentPrograms()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitedBy = Uuid.CreateVersion4();
        var programIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var store = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();
        var issuer = new TenantInvitationIssuer(
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
            EmailChallengeTokenKeys.FromConfiguration(configuration, true), TimeProvider.System);
        var handler = new InviteGuestConsultantHandler(issuer,
            new ExistingProgramScopes(tenantId, programIds));
        var expiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var request = new RequestContext<InviteGuestConsultant>(new InviteGuestConsultant(
            tenantId, "guest@example.com", programIds, expiresOn), Actor(invitedBy));

        // Act
        var result = await handler.HandleAsync(request, CancellationToken.None);
        var aggregate = new TenantInvitation(tenantId, "guest@example.com");
        var recorded = new List<DomainEvent>();
        await foreach (var item in store.ReadAsync(aggregate.Stream, 0))
            recorded.Add(item.Event);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var invited = Assert.IsType<TenantMemberInvited>(Assert.Single(recorded));
        Assert.Equal("guest", invited.Affiliation);
        Assert.Equal(invitedBy, invited.InvitedBy);
        Assert.Equal(invitedBy.ToString(), invited.InvitedByDisplay);
        Assert.Equal(expiresOn, invited.GuestExpiresOn);
        Assert.Equal(programIds.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(),
            invited.GuestProgramIds);
        Assert.False(invited.Administrator);
        Assert.Null(invited.BuiltInRole);
    }

    [Fact]
    public async Task ShouldRejectGuestInvitationGivenProgramIsOutsideSelectedTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = new InviteGuestConsultantHandler(null!,
            new ExistingProgramScopes(tenantId, []));
        var request = new RequestContext<InviteGuestConsultant>(new InviteGuestConsultant(
            tenantId, "guest@example.com", [programId], DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30)),
            Actor(Uuid.CreateVersion4()));

        // Act
        var result = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

    sealed class ExistingProgramScopes(Uuid tenantId, IReadOnlyCollection<Uuid> programIds)
        : IProgramResourceScopeResolver
    {
        public ValueTask<bool> IsTenantProgramAsync(Uuid requestedTenantId, Uuid programId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(requestedTenantId == tenantId && programIds.Contains(programId));

        public ValueTask<bool> IsTenantApplicationAsync(Uuid requestedTenantId, Uuid applicationId,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<Uuid?> ResolveProgramIdAsync(Uuid requestedTenantId,
            IProgramResourceRequest request, CancellationToken ct = default) =>
            ValueTask.FromResult<Uuid?>(null);
    }
}
