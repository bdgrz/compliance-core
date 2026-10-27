using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class MemberTenantDiscoveryLagTests
{
    [Fact]
    public async Task ShouldHideTenantMetadataGivenGuestExpiryBeforeMembershipProjection()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        await SeedTenantAsync(writer, tenantId, userId, "Expired Guest Org", "expired-guest-org",
            "guest", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));
        var activeTenants = new ActiveTenants(tenantId);
        var projectedMemberships = new FixedMembershipDirectory(true);
        var projectedTenants = new TenantViews(
            new TenantView(tenantId, "Expired Guest Org", "expired-guest-org"));
        var list = new ListMyTenantsHandler(activeTenants, projectedMemberships,
            projectedTenants, reader);
        var resolve = new ResolveMyTenantSlugHandler(reader, projectedMemberships);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

        // Act
        var tenants = await list.HandleAsync(new RequestContext<ListMyTenants>(new ListMyTenants(),
            actor), CancellationToken.None);
        var slug = await resolve.HandleAsync(new RequestContext<ResolveMyTenantSlug>(
            new ResolveMyTenantSlug("expired-guest-org"), actor), CancellationToken.None);

        // Assert
        Assert.True(await projectedMemberships.IsMemberAsync(tenantId.ToString(), userId));
        Assert.Empty(tenants.Value.Items);
        Assert.Equal(RequestErrorKind.NotFound, slug.Error?.Kind);
    }

    [Fact]
    public async Task ShouldHideTenantMetadataGivenSuspensionBeforeMembershipProjection()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var suspendedTenantId = Uuid.CreateVersion4();
        var activeTenantId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        await SeedTenantAsync(writer, suspendedTenantId, userId, "Suspended", "suspended-org",
            "client_personnel");
        await SeedTenantAsync(writer, activeTenantId, userId, "Active", "active-org",
            "firm_staff");
        var activeTenants = new ActiveTenants(suspendedTenantId, activeTenantId);
        var projectedMemberships = new FixedMembershipDirectory(true);
        var projectedTenants = new TenantViews(
            new TenantView(suspendedTenantId, "Suspended", "suspended-org"),
            new TenantView(activeTenantId, "Active", "active-org"));
        var list = new ListMyTenantsHandler(activeTenants, projectedMemberships,
            projectedTenants, reader);
        var resolve = new ResolveMyTenantSlugHandler(reader, projectedMemberships);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
        var before = await list.HandleAsync(new RequestContext<ListMyTenants>(new ListMyTenants(),
            actor), CancellationToken.None);
        Assert.True(before.IsSuccess);
        Assert.Equal(2, before.Value.Items.Count);

        // Act: the source event commits; the membership projection deliberately remains active.
        var member = await reader.HydrateAsync(new Member(suspendedTenantId, userId));
        Assert.True(member.Suspend(RbacIds.Member(suspendedTenantId, Uuid.CreateVersion4()),
            "Administrator", DateTimeOffset.UtcNow, "Access review.").IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var after = await list.HandleAsync(new RequestContext<ListMyTenants>(new ListMyTenants(),
            actor), CancellationToken.None);
        var suspendedSlug = await resolve.HandleAsync(new RequestContext<ResolveMyTenantSlug>(
            new ResolveMyTenantSlug("suspended-org"), actor), CancellationToken.None);
        var activeSlug = await resolve.HandleAsync(new RequestContext<ResolveMyTenantSlug>(
            new ResolveMyTenantSlug("active-org"), actor), CancellationToken.None);

        // Assert
        Assert.True(await projectedMemberships.IsMemberAsync(suspendedTenantId.ToString(), userId));
        Assert.True(after.IsSuccess);
        Assert.Equal(activeTenantId, Assert.Single(after.Value.Items).TenantId);
        Assert.False(suspendedSlug.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, suspendedSlug.Error?.Kind);
        Assert.True(activeSlug.IsSuccess);
        Assert.Equal(activeTenantId, activeSlug.Value.TenantId);
    }

    static async Task SeedTenantAsync(IAggregateWriter writer, Uuid tenantId, Uuid userId,
        string name, string slug, string affiliation, DateOnly? membershipExpiresOn = null)
    {
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(userId, name, slug).IsSuccess);
        Assert.True(tenant.ConfirmSlug(slug).IsSuccess);
        await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
        var reservation = new TenantSlug(slug);
        Assert.True(reservation.Register(tenantId).IsSuccess);
        await writer.SaveAsync(reservation, new RequestDispatchContext(RequestActor.System));
        var member = new Member(tenantId, userId);
        Assert.True(member.Register(affiliation, membershipExpiresOn).IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
    }

    sealed class ActiveTenants(params Uuid[] tenantIds) : ITenantDirectory
    {
        public async IAsyncEnumerable<TenantId> GetActiveTenantsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var tenantId in tenantIds)
                yield return new TenantId(tenantId.ToString());
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<TenantLifecycleChange> WatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    sealed class TenantViews(params TenantView[] tenants) : ITenantDirectoryReader
    {
        public ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenants.SingleOrDefault(tenant => tenant.TenantId == tenantId));

        public ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantView>(tenants, null));
    }
}
