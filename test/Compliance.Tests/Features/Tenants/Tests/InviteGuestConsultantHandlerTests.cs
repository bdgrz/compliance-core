using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class InviteGuestConsultantHandlerTests
{
    [Fact]
    public async Task ShouldIssueScopedGuestInvitationGivenTenantAdminSelectsCurrentPrograms()
    {
        // Arrange
        var programIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        await using var host = new TenantInvitationHost(programs: programIds);
        var invitationId = Uuid.CreateVersion4();
        var expiresOn = DateOnly.FromDateTime(TenantInvitationHost.At.UtcDateTime).AddDays(30);

        // Act
        var result = await host.InviteGuestAsync(invitationId, "guest@example.com", programIds, expiresOn);
        var facts = await host.InvitationFactsAsync(invitationId);
        var scope = await host.Content.ReadJsonAsync<IReadOnlyList<Uuid>>(host.TenantId.ToString(),
            facts.OfType<TenantMemberInvited>().Single().GuestProgramIdsManifest!);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var invited = Assert.IsType<TenantMemberInvited>(Assert.Single(facts));
        Assert.Equal("guest", invited.Affiliation);
        Assert.Equal(host.Inviter, invited.InvitedBy);
        Assert.Equal(host.Inviter.ToString(), invited.InvitedByDisplay);
        Assert.Equal(expiresOn, invited.GuestExpiresOn);
        Assert.Equal(programIds.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(), scope);
        Assert.False(invited.Administrator);
        Assert.Null(invited.BuiltInRole);
    }

    [Fact]
    public async Task ShouldRejectGuestInvitationGivenProgramIsOutsideSelectedTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = new InviteGuestConsultantHandler(null!, new ExistingProgramScopes(tenantId, []));
        var request = new RequestContext<InviteGuestConsultant>(new InviteGuestConsultant(Uuid.CreateVersion4(),
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
