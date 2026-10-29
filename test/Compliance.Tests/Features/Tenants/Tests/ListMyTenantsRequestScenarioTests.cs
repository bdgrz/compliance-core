using System.Security.Claims;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

/// <summary>
///     Runs <see cref="ListMyTenants" /> through Portia's real composed lifecycle — confirms
///     <see cref="ListMyTenantsAuthorizer" /> is actually wired to it, and that the handler resolves
///     only the calling user's own memberships by fanning out across active tenants.
/// </summary>
public sealed class ListMyTenantsRequestScenarioTests
{
    static readonly Uuid CallerUserId = Uuid.CreateVersion4();
    static readonly Uuid OtherUserId = Uuid.CreateVersion4();
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OtherTenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldDenyGivenAnActorWithoutABdgrzIdentity()
    {
        // Arrange
        await using var provider = BuildProvider(
            new FakeTenantDirectory(), new FakeTenantMembershipDirectoryReader(), new FakeTenantDirectoryReader());

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.Anonymous)
            // Act
            .When(new ListMyTenants())
            // Assert
            .ExpectDenied(RequestErrorKind.Unauthorized)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldReturnOwnTenantsGivenMemberQuery()
    {
        // Arrange
        var activeTenants = new FakeTenantDirectory(TenantId, OtherTenantId);
        var memberships = new FakeTenantMembershipDirectoryReader();
        memberships.Members[TenantId] = [CallerUserId];
        memberships.Members[OtherTenantId] = [OtherUserId];
        var tenants = new FakeTenantDirectoryReader();
        tenants.ById[TenantId] = new TenantView(TenantId, "Acme", "acme");
        tenants.ById[OtherTenantId] = new TenantView(OtherTenantId, "Other", "other");
        await using var provider = BuildProvider(activeTenants, memberships, tenants);
        await SeedMemberAsync(provider, TenantId, CallerUserId);

        // Act
        var scenario = await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(CallerUserId))
            .When(new ListMyTenants())
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();

        // Assert
        var summary = Assert.Single(scenario.Value.Items);
        Assert.Equal(TenantId, summary.TenantId);
        Assert.Equal("Acme", summary.Name);
        Assert.Equal("acme", summary.Slug);
    }

    [Fact]
    public async Task ShouldPaginateTenantsGivenReturnedCursor()
    {
        // Arrange
        var tenantIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var activeTenants = new FakeTenantDirectory(tenantIds);
        var memberships = new FakeTenantMembershipDirectoryReader();
        memberships.Members[tenantIds[0]] = [CallerUserId];
        memberships.Members[tenantIds[1]] = [CallerUserId];
        memberships.Members[tenantIds[2]] = [CallerUserId];
        var tenants = new FakeTenantDirectoryReader();
        foreach (var tenantId in tenantIds)
        {
            tenants.ById[tenantId] = new TenantView(tenantId, tenantId.ToString(), tenantId.ToString());
        }

        await using var provider = BuildProvider(activeTenants, memberships, tenants);
        foreach (var tenantId in tenantIds)
            await SeedMemberAsync(provider, tenantId, CallerUserId);
        var seen = new List<Uuid>();
        string? cursor = null;

        // Act
        do
        {
            var scenario = await RequestScenario.For(provider)
                .GivenActor(BdgrzActor(CallerUserId))
                .When(new ListMyTenants(Limit: 1, Cursor: cursor))
                .ExpectSuccess();
            seen.AddRange(scenario.Value.Items.Select(item => item.TenantId));
            cursor = scenario.Value.NextCursor;
        } while (cursor is not null);

        // Assert
        Assert.Equal(tenantIds.OrderBy(id => id.ToString(), StringComparer.Ordinal), seen);
    }

    [Fact]
    public async Task ShouldSkipMembershipGivenTenantProjectionLag()
    {
        // Arrange
        var activeTenants = new FakeTenantDirectory(TenantId);
        var memberships = new FakeTenantMembershipDirectoryReader();
        memberships.Members[TenantId] = [CallerUserId];
        var tenants = new FakeTenantDirectoryReader();
        await using var provider = BuildProvider(activeTenants, memberships, tenants);
        await SeedMemberAsync(provider, TenantId, CallerUserId);

        // Act
        var scenario = await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(CallerUserId))
            .When(new ListMyTenants())
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();

        // Assert
        Assert.Empty(scenario.Value.Items);
    }

    static ServiceProvider BuildProvider(
        ITenantDirectory activeTenants,
        ITenantMembershipDirectoryReader memberships,
        ITenantDirectoryReader tenants)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(activeTenants);
        services.AddSingleton(memberships);
        services.AddSingleton(tenants);
        services.AddPortia()
            .AddRequestHandler<ListMyTenantsHandler>()
            .AddRequestAuthorizer<ListMyTenantsAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static async Task SeedMemberAsync(ServiceProvider provider, Uuid tenantId, Uuid userId)
    {
        await using var scope = provider.CreateAsyncScope();
        var member = new Member(tenantId, userId);
        Assert.True(member.Register().IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
            .SaveAsync(member, new RequestDispatchContext(RequestActor.System));
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class FakeTenantDirectory(params Uuid[] activeTenants) : ITenantDirectory
    {
        public async IAsyncEnumerable<TenantId> GetActiveTenantsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var tenantId in activeTenants)
            {
                yield return new TenantId(tenantId.ToString());
            }

            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<TenantLifecycleChange> WatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    sealed class FakeTenantMembershipDirectoryReader : ITenantMembershipDirectoryReader
    {
        public async ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => await IsMemberAsync(tenantId, userId, ct)
            ? new TenantMembershipView(userId,
                Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture)) : null;

        public Dictionary<Uuid, List<Uuid>> Members { get; } = [];

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(
                Members.TryGetValue(Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture), out var members) &&
                members.Contains(userId));

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class FakeTenantDirectoryReader : ITenantDirectoryReader
    {
        public Dictionary<Uuid, TenantView> ById { get; } = [];

        public ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(ById.TryGetValue(tenantId, out var tenant) ? tenant : null);

        public ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantView>([], null));
    }
}
