using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantCreatorBootstrapTests
{
    [Fact]
    public void ShouldReplayOriginalRegistrationGivenAddressChangedAfterCreation()
    {
        // Arrange
        var tenant = new Tenant(Uuid.CreateVersion4());
        var creator = Uuid.CreateVersion4();
        Assert.True(tenant.Register(creator, "Company", "company", "Company LLC", creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.ConfirmSlug("company").IsSuccess);
        Assert.True(tenant.Activate(creator, null).IsSuccess);
        Assert.True(tenant.RequestSlugChange("new-company").IsSuccess);
        Assert.True(tenant.ConfirmSlug("new-company").IsSuccess);
        var scenario = new AggregateScenario<Tenant>(tenant);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var exact = tenant.Register(creator, " Company ", "COMPANY", "Company LLC", creatorIsAdministrator: true);
        var changed = tenant.Register(creator, "Another Company", "company", "Company LLC", creatorIsAdministrator: true);

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.Equal("company", exact.Value.Slug);
        Assert.Equal("new-company", tenant.CurrentSlug);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecoverInitialAddressGivenRejectedClaim()
    {
        // Arrange
        var tenant = new Tenant(Uuid.CreateVersion4());
        var creator = Uuid.CreateVersion4();
        Assert.True(tenant.Register(creator, "Company", "company", creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.RejectSlug("company").IsSuccess);

        // Act
        var recovery = tenant.RequestSlugChange("company-next");
        var statusWhilePending = tenant.GetProvisioningStatus();
        var competing = tenant.RequestSlugChange("company-other");
        var confirmation = tenant.ConfirmSlug("company-next");

        // Assert
        Assert.True(tenant.IsRegistered);
        Assert.True(recovery.IsSuccess);
        Assert.Equal("provisioning", statusWhilePending.Status);
        Assert.False(competing.IsSuccess);
        Assert.True(confirmation.IsSuccess);
        Assert.Equal("company-next", tenant.CurrentSlug);
        Assert.True(tenant.CanActivateCreator);
        Assert.False(tenant.IsActive);
        Assert.True(tenant.Activate(creator, null).IsSuccess);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void ShouldKeepRecoveryAvailableGivenReplacementAddressAlsoRejected()
    {
        // Arrange
        var tenant = new Tenant(Uuid.CreateVersion4());
        Assert.True(tenant.Register(Uuid.CreateVersion4(), "Company", "company", creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.RejectSlug("company").IsSuccess);
        Assert.True(tenant.RequestSlugChange("company-next").IsSuccess);

        // Act
        Assert.True(tenant.RejectSlug("company-next").IsSuccess);
        var next = tenant.RequestSlugChange("company-final");

        // Assert
        Assert.True(next.IsSuccess);
        Assert.False(tenant.IsActive);
    }

    [Fact]
    public void ShouldRefuseAnotherInitialClaimGivenFirstClaimStillPending()
    {
        // Arrange
        var tenant = new Tenant(Uuid.CreateVersion4());
        Assert.True(tenant.Register(Uuid.CreateVersion4(), "Company", "company", creatorIsAdministrator: true).IsSuccess);
        var scenario = new AggregateScenario<Tenant>(tenant);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var result = tenant.RequestSlugChange("company-next");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldAuthorizeOnlyCreatorWithoutProjectionGivenInitialAddressRecovery()
    {
        // Arrange
        var creator = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creator, "Company", "company", creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.RejectSlug("company").IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
            .SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
        var authorizer = new PlatformOperatorOrTenantAdminAuthorizer(new FixedOperatorAccess(),
            new UnavailableMemberships(), new UnavailableActivity(), new UnavailablePermissions(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var request = new ChangeTenantSlug(Uuid.CreateVersion4(), tenantId, "company-next", 2);

        // Act
        var creatorResult = await authorizer.AuthorizeAsync(
            new RequestContext<IPlatformOperatorOrTenantAdminRequest>(request, Actor(creator)), CancellationToken.None);
        var otherResult = await authorizer.AuthorizeAsync(
            new RequestContext<IPlatformOperatorOrTenantAdminRequest>(request, Actor(Uuid.CreateVersion4())), CancellationToken.None);
        Assert.True(tenant.RequestSlugChange("company-next").IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
            .SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
        var pendingRetry = await authorizer.AuthorizeAsync(
            new RequestContext<IPlatformOperatorOrTenantAdminRequest>(request, Actor(creator)), CancellationToken.None);

        // Assert
        Assert.True(creatorResult.IsSuccess);
        Assert.True(pendingRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, otherResult.Error?.Kind);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class UnavailableMemberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Initial address recovery must not depend on a membership projection.");
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Initial address recovery must not depend on a membership projection.");
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new InvalidOperationException("Initial address recovery must not depend on a membership projection.");
    }

    sealed class UnavailableActivity : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            throw new InvalidOperationException("An organization recovering its first address is not active yet.");
    }

    sealed class UnavailablePermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            throw new InvalidOperationException("The creator has this single recovery command before standing tenant authority exists.");
    }
}
