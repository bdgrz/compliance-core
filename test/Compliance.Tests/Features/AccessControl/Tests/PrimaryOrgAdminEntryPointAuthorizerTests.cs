using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PrimaryOrgAdminEntryPointAuthorizerTests
{
    [Theory]
    [InlineData("completed", true, true, true)]
    [InlineData("pending", true, true, false)]
    [InlineData("released", true, true, false)]
    [InlineData("ordinary_staff", true, true, false)]
    [InlineData("suspended", true, true, false)]
    [InlineData("guest", true, true, false)]
    [InlineData("completed", false, true, false)]
    [InlineData("completed", true, false, false)]
    public async Task ShouldPermitStaffEntryOnlyGivenCurrentCompletedPrimaryAndOrdinaryChecks(
        string state, bool activeTenant, bool permission, bool expected)
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = new Member(tenant, user);
        Assert.True(state == "guest"
            ? member.Register("guest", new DateOnly(2027, 1, 1), programIds: [Uuid.CreateVersion4()]).IsSuccess
            : member.Register("firm_staff").IsSuccess);
        if (state == "suspended")
            Assert.True(member.Suspend(Uuid.CreateVersion4(), "Administrator",
                new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "Access review").IsSuccess);
        var designation = Uuid.CreateVersion4();
        var primaries = new PrimaryOrgAdmins(tenant);
        if (state is "completed" or "pending" or "released")
        {
            Assert.True(primaries.Designate(designation, user, member.MembershipEpisodeId).IsSuccess);
            Assert.True(member.MarkPrimary(designation, member.MembershipEpisodeId).IsSuccess);
            if (state != "pending")
                Assert.True(primaries.RecordOutcome(designation, user, member.MembershipEpisodeId, null).IsSuccess);
            if (state == "released")
            {
                var replacement = Uuid.CreateVersion4();
                var replacementEpisode = Uuid.CreateVersion4();
                var replacementDesignation = Uuid.CreateVersion4();
                Assert.True(primaries.Designate(replacementDesignation, replacement, replacementEpisode).IsSuccess);
                Assert.True(primaries.RecordOutcome(replacementDesignation, replacement, replacementEpisode, null).IsSuccess);
                Assert.True(primaries.Release(Uuid.CreateVersion4(), user, designation, "Replacement confirmed").IsSuccess);
            }
        }
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        if (primaries.SeatCount != 0)
            await writer.SaveAsync(primaries, new RequestDispatchContext(RequestActor.System));
        // The directory can still show active staff while current owners have changed.
        var memberships = new StaffMemberships(tenant);
        var permissions = new Permissions(permission);
        var activity = new Activity(activeTenant);
        var rbac = new RbacManagementAuthorizer(permissions, activity, memberships, reader);
        var get = new GetTenantAuthorizer(new NoOperators(), memberships, activity, permissions,
            sourceReader: reader);
        var address = new PlatformOperatorOrTenantAdminAuthorizer(new NoOperators(), memberships, activity, permissions, reader);
        var actor = Actor(user);

        // Act
        var manage = await rbac.AuthorizeTenantAsync(actor, tenant, CancellationToken.None);
        var read = await get.AuthorizeAsync(new RequestContext<GetTenant>(new GetTenant(tenant), actor),
            CancellationToken.None);

        var changeAddress = await address.AuthorizeAsync(new RequestContext<IPlatformOperatorOrTenantAdminRequest>(
            new ChangeTenantSlug(Uuid.CreateVersion4(), tenant, "new-address", 1), actor), CancellationToken.None);

        // Assert
        Assert.Equal(expected, changeAddress.IsSuccess);
        Assert.Equal(expected, manage.IsSuccess);
        Assert.Equal(expected, read.IsSuccess);
    }

    [Fact]
    public async Task ShouldKeepStaffWallGivenNoSourceReader()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var memberships = new StaffMemberships(tenant);
        var permissions = new Permissions(true);
        var activity = new Activity(true);
        var rbac = new RbacManagementAuthorizer(permissions, activity, memberships);
        var get = new GetTenantAuthorizer(new NoOperators(), memberships, activity, permissions);

        // Act
        var manage = await rbac.AuthorizeTenantAsync(Actor(user), tenant, CancellationToken.None);
        var read = await get.AuthorizeAsync(new RequestContext<GetTenant>(new GetTenant(tenant), Actor(user)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, manage.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, read.Error?.Kind);
    }

    static ClaimsPrincipal Actor(Uuid user) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "BdgrzSession"));

    static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        return services.BuildServiceProvider();
    }

    sealed class StaffMemberships(Uuid tenant) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(new(userId, tenant, "firm_staff"));
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class Permissions(bool permitted) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permitted);
    }

    sealed class Activity(bool active) : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(active);
    }

    sealed class NoOperators : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(false);
    }
}
