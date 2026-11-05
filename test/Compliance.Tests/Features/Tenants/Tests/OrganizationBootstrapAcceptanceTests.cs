using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Reactors;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Reactors;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

/// <summary>
/// Exercises the production request composition, authoritative permission evaluator, registered reactions and
/// tenant/member projectors. In-memory event and KV persistence are controlled seams, not provider qualification.
/// Authentication has already established the canonical user; the email directory supplies that user's verified email.
/// </summary>
public sealed class OrganizationBootstrapAcceptanceTests
{
    [Fact]
    public async Task ShouldCreateUsableOrganizationGivenVerifiedCreatorAndCompletedBootstrap()
    {
        // Arrange
        await using var host = new Host();
        var tenant = await host.CreateOrganizationAsync();

        // Act
        await host.BootstrapAsync(tenant);
        var owner = await host.ReadAsync(new Tenant(tenant));
        var member = await host.ReadAsync(new Member(tenant, host.Creator));
        var primaries = await host.SendAsync(new GetPrimaryOrgAdmins(tenant), host.Actor);
        var directory = await host.SendAsync(new GetTenant(tenant), host.Actor);
        var membership = await host.SendAsync(new GetTenantMember(tenant, host.Creator), host.Actor);
        var members = await host.SendAsync(new ListTenantMembers(tenant), host.Actor);
        var provisioning = await host.SendAsync(new GetTenantProvisioningStatus(tenant), host.Actor);
        var teamId = Uuid.CreateVersion4();
        var subsequent = await host.SendAsync(new DefineTeam(tenant, teamId, "First working team"), host.Actor);

        // Assert
        Assert.True(owner.IsActive);
        Assert.True(member.IsPrimary);
        Assert.True(primaries.IsSuccess, primaries.Error?.Message);
        var primary = Assert.Single(primaries.Value.Seats);
        Assert.True(primary.IsDesignated);
        Assert.Equal(host.Creator, primary.UserId);
        Assert.Equal(member.MembershipEpisodeId, primary.MembershipEpisodeId);
        Assert.Equal(member.PrimaryDesignationId, primary.DesignationId);
        Assert.True(directory.IsSuccess, directory.Error?.Message);
        Assert.Equal("active", directory.Value.Status);
        Assert.True(membership.IsSuccess, membership.Error?.Message);
        Assert.Equal(host.Creator, membership.Value.UserId);
        Assert.True(membership.Value.IsPrimary);
        Assert.Equal(member.MembershipEpisodeId, membership.Value.MembershipEpisodeId);
        Assert.True(members.IsSuccess, members.Error?.Message);
        Assert.True(Assert.Single(members.Value.Items).IsPrimary);
        Assert.True(provisioning.IsSuccess, provisioning.Error?.Message);
        Assert.Equal("completed", provisioning.Value.Status);
        Assert.True(subsequent.IsSuccess, subsequent.Error?.Message);
        Assert.True((await host.ReadAsync(new Team(tenant, teamId))).IsActive);
    }

    [Fact]
    public async Task ShouldActivateFromCommittedPrimaryGivenSlugConfirmationArrivesLast()
    {
        // Arrange
        await using var host = new Host();
        var tenant = await host.CreateOrganizationAsync();
        await host.CompletePrimaryBeforeSlugAsync(tenant);
        Assert.False((await host.ReadAsync(new Tenant(tenant))).IsActive);
        Assert.True((await host.ReadAsync(new Member(tenant, host.Creator))).IsPrimary);

        // Act
        await host.BootstrapAsync(tenant);
        var organization = await host.SendAsync(new GetTenant(tenant), host.Actor);

        // Assert
        Assert.True((await host.ReadAsync(new Tenant(tenant))).IsActive);
        Assert.True(organization.IsSuccess, organization.Error?.Message);
        Assert.Equal("active", organization.Value.Status);
    }

    [Fact]
    public async Task ShouldReplayCreationAndRefuseChangedIntentGivenStableTenantIdentity()
    {
        // Arrange
        await using var host = new Host();
        var tenant = Uuid.CreateVersion4();
        var request = new RegisterTenant(tenant, "Acme", $"retry-{tenant.ToString()[..8]}", "Acme LLC");
        var first = await host.SendAsync(request, host.Actor);
        Assert.True(first.IsSuccess, first.Error?.Message);
        await host.BootstrapAsync(tenant);
        var before = await host.ReadAsync(new Tenant(tenant));

        // Act
        var exact = await host.SendAsync(request, host.Actor);
        var changed = await host.SendAsync(request with { Name = "Different organization" }, host.Actor);
        var after = await host.ReadAsync(new Tenant(tenant));

        // Assert
        Assert.True(exact.IsSuccess, exact.Error?.Message);
        Assert.Equal(first.Value, exact.Value);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(tenant, exact.Value.TenantId);
    }

    [Fact]
    public async Task ShouldPreservePrimaryAuthorityGivenNoTeamMembershipAndReplayedBootstrap()
    {
        // Arrange
        await using var host = new Host();
        var tenant = await host.CreateOrganizationAsync();
        await host.BootstrapAsync(tenant);
        var before = await host.ReadAsync(new Member(tenant, host.Creator));
        var memberId = RbacIds.Member(tenant, host.Creator);
        await host.MutateAsync(new TeamMember(tenant, BuiltInRbac.AdministratorsTeamId(tenant), memberId),
            owner => owner.Remove());

        // Act
        await host.BootstrapAsync(tenant, replay: true);
        var after = await host.ReadAsync(new Member(tenant, host.Creator));
        var explanation = await host.SendAsync(new GetMemberAccess(tenant, host.Creator), host.Actor);
        var subsequent = await host.SendAsync(new DefineTeam(tenant, Uuid.CreateVersion4(), "Primary-owned team"), host.Actor);

        // Assert
        Assert.Equal(before.PrimaryDesignationId, after.PrimaryDesignationId);
        Assert.True(explanation.IsSuccess, explanation.Error?.Message);
        Assert.Equal(before.PrimaryDesignationId, explanation.Value.PrimaryDesignationId);
        Assert.Contains(RbacPermissions.TenantRbacManage, explanation.Value.EffectivePermissions);
        Assert.Empty(explanation.Value.Paths);
        Assert.Empty(explanation.Value.GrantPaths);
        Assert.True(subsequent.IsSuccess, subsequent.Error?.Message);
        Assert.False((await host.ReadAsync(new TeamMember(tenant,
            BuiltInRbac.AdministratorsTeamId(tenant), memberId))).IsAssigned);
    }

    [Fact]
    public async Task ShouldAllowSelfReleaseOnlyAfterReplacementGivenTwoPhaseDesignation()
    {
        // Arrange
        await using var host = new Host();
        var tenant = await host.CreateOrganizationAsync();
        await host.BootstrapAsync(tenant);
        var first = await host.ReadAsync(new Member(tenant, host.Creator));
        var replacement = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RegisterMember(episode, tenant, replacement), RequestActor.System)).IsSuccess);
        await host.ProjectMembershipAsync(tenant);
        var designation = new DesignatePrimaryOrgAdmin(Uuid.CreateVersion4(), tenant, replacement, episode);
        Assert.True((await host.SendAsync(designation, host.Actor)).IsSuccess);
        var release = new ReleasePrimaryOrgAdmin(Uuid.CreateVersion4(), tenant, host.Creator,
            first.PrimaryDesignationId, "Replacement accepted");

        // Act
        var pendingRelease = await host.SendAsync(release, host.Actor);
        await host.CompleteDesignationsAsync(tenant);
        var completedRelease = await host.SendAsync(release, host.Actor);
        await host.CompleteDesignationsAsync(tenant);
        var oldMember = await host.ReadAsync(new Member(tenant, host.Creator));
        var newMember = await host.ReadAsync(new Member(tenant, replacement));
        var retained = await host.ReadAsync(new PrimaryOrgAdmins(tenant));
        var replacementActor = Host.ActorOf(replacement);
        var replacementWork = await host.SendAsync(new DefineTeam(tenant, Uuid.CreateVersion4(), "Replacement's team"), replacementActor);

        // Assert
        Assert.False(pendingRelease.IsSuccess);
        Assert.True(completedRelease.IsSuccess, completedRelease.Error?.Message);
        Assert.False(oldMember.IsPrimary);
        Assert.True(newMember.IsPrimary);
        Assert.Equal(1, retained.DesignatedCount);
        Assert.True(retained.HasDesignated(replacement, episode, designation.CommandId));
        Assert.True(replacementWork.IsSuccess, replacementWork.Error?.Message);
    }

    [Fact]
    public async Task ShouldRefuseThirdSeatAndWrongTenantGivenCurrentAdministrator()
    {
        // Arrange
        await using var host = new Host();
        var tenant = await host.CreateOrganizationAsync();
        await host.BootstrapAsync(tenant);
        var replacement = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RegisterMember(episode, tenant, replacement), RequestActor.System)).IsSuccess);
        Assert.True((await host.SendAsync(new DesignatePrimaryOrgAdmin(Uuid.CreateVersion4(), tenant,
            replacement, episode), host.Actor)).IsSuccess);

        // Act
        var third = await host.SendAsync(new DesignatePrimaryOrgAdmin(Uuid.CreateVersion4(), tenant,
            Uuid.CreateVersion4(), Uuid.CreateVersion4()), host.Actor);
        var otherTenant = await host.SendAsync(new DesignatePrimaryOrgAdmin(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), host.Creator, episode), host.Actor);
        var stranger = await host.SendAsync(new ReleasePrimaryOrgAdmin(Uuid.CreateVersion4(), tenant,
            host.Creator, (await host.ReadAsync(new Member(tenant, host.Creator))).PrimaryDesignationId,
            "Unauthorized"), Host.ActorOf(Uuid.CreateVersion4()));

        // Assert
        Assert.False(third.IsSuccess);
        Assert.False(otherTenant.IsSuccess);
        Assert.False(stranger.IsSuccess);
        Assert.Equal(2, (await host.ReadAsync(new PrimaryOrgAdmins(tenant))).SeatCount);
    }

    [Fact]
    public async Task ShouldDenyInternalProtocolCommandsGivenAuthenticatedPrimaryAdministrator()
    {
        // Arrange
        await using var host = new Host();
        var tenant = await host.CreateOrganizationAsync();
        await host.BootstrapAsync(tenant);
        var primary = await host.ReadAsync(new Member(tenant, host.Creator));

        // Act
        var mark = await host.SendAsync(new MarkMemberPrimary(tenant, host.Creator,
            primary.MembershipEpisodeId, primary.PrimaryDesignationId), host.Actor);
        var unmark = await host.SendAsync(new UnmarkMemberPrimary(tenant, host.Creator,
            primary.MembershipEpisodeId, primary.PrimaryDesignationId), host.Actor);
        var outcome = await host.SendAsync(new RecordPrimaryMarkOutcome(tenant, host.Creator,
            primary.MembershipEpisodeId, primary.PrimaryDesignationId), host.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, mark.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, unmark.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, outcome.Error?.Kind);
        Assert.True((await host.ReadAsync(new Member(tenant, host.Creator))).IsPrimary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDenyCreationGivenUnverifiedOrAnonymousIdentity(bool anonymous)
    {
        // Arrange
        await using var host = new Host(verified: false);

        // Act
        var result = await host.SendAsync(new RegisterTenant(Uuid.CreateVersion4(), "Acme", "unverified", "Acme LLC"),
            anonymous ? RequestActor.Anonymous : host.Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(anonymous ? RequestErrorKind.Unauthorized : RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    sealed class Host : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly Dictionary<string, ProjectionCheckpoint> _projectorCheckpoints = [];
        public Uuid Creator { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal Actor => ActorOf(Creator);

        public Host(bool verified = true)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://127.0.0.1:1/ws",
                ["Fitz:ApplicationName"] = "organization-bootstrap-acceptance",
                ["Cassie:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=postgres;Username=root;Password=test;SSL Mode=Disable",
            }).Build());
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
            services.RemoveAll<IEmailAddressDirectoryReader>();
            services.AddSingleton<IEmailAddressDirectoryReader>(new VerifiedEmailDirectory(Creator, verified));
            // Optional presentation enrichment is outside this source/bootstrap contract. It supplies no authority.
            services.RemoveAll<IPersonMemberDisplayReader>();
            services.RemoveAll<IUserDisplayNameReader>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public static ClaimsPrincipal ActorOf(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

        public async Task<Uuid> CreateOrganizationAsync()
        {
            var result = await SendAsync(new RegisterTenant(Uuid.CreateVersion4(), "Acme", $"bootstrap-{Creator.ToString()[..8]}", "Acme LLC"), Actor);
            Assert.True(result.IsSuccess, result.Error?.Message);
            return result.Value.TenantId;
        }

        public async Task BootstrapAsync(Uuid tenant, bool replay = false)
        {
            // Run slug reservation/confirmation first. Primary completion must activate the tenant from source facts.
            await ReactAsync<TenantRegistrationReactor>(tenant);
            await ReactAsync<TenantSlugReactor>(tenant);
            await ReactAsync<TenantRbacBootstrapReactor>(tenant, replay);
            await CompleteDesignationsAsync(tenant);
            await ReactAsync<TenantSelfServiceActivationReactor>(tenant);
            await ProjectAsync("TenantDirectory", tenant);
            await ProjectMembershipAsync(tenant);
        }

        public async Task CompletePrimaryBeforeSlugAsync(Uuid tenant)
        {
            await ReactAsync<TenantRbacBootstrapReactor>(tenant);
            await CompleteDesignationsAsync(tenant);
        }

        public async Task CompleteDesignationsAsync(Uuid tenant)
        {
            await ReactAsync<PrimaryOrgAdminReactor>(tenant);
            await ReactAsync<PrimaryMemberOutcomeReactor>(tenant);
            await ProjectMembershipAsync(tenant);
        }

        public Task ProjectMembershipAsync(Uuid tenant) => ProjectAsync("TenantMembership", tenant);

        public async Task<T> ReadAsync<T>(T owner) where T : Aggregate
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(owner);
        }

        public async Task MutateAsync<T>(T owner, Func<T, Result> mutation) where T : Aggregate
        {
            await using var scope = _provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner,
                item => AggregateOutcome.CommitOnSuccess(mutation(item)), new RequestDispatchContext(RequestActor.System));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        public async Task<Result> SendAsync(IRequest request, ClaimsPrincipal actor)
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request, actor);
        }

        public async Task<Result<T>> SendAsync<T>(IRequest<T> request, ClaimsPrincipal actor)
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request, actor);
        }

        async Task ReactAsync<T>(Uuid tenant, bool replay = false) where T : Reactor
        {
            await using var scope = _provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), item => item.ComponentType == typeof(T));
            var reactor = services.GetRequiredService<T>();
            if (registration.Scope == WorkloadScope.PerTenant)
                await new ReactorScenario(new TenantId(tenant.ToString())).RunAsync(reactor);
            var checkpoints = services.GetRequiredService<IProjectionCheckpointStore>();
            var checkpoint = replay ? ProjectionCheckpoint.Start :
                await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));
            var runner = new ReactorRunner(services.GetRequiredService<IDomainEventReader>(),
                services.GetRequiredService<IReactorPrincipalProvider>(), services.GetRequiredService<TimeProvider>());
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                var next = await runner.RunAsync(reactor, checkpoint, ct: deadline.Token);
                if (next == checkpoint)
                    return;
                checkpoint = next;
            }
        }

        async Task ProjectAsync(string name, Uuid tenant)
        {
            await using var scope = _provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), item => item.Name == name);
            var projector = (Projector)services.GetRequiredService(registration.ComponentType);
            if (registration.Scope == WorkloadScope.PerTenant)
                await new ProjectorScenario(new TenantId(tenant.ToString())).RunAsync(projector);
            var key = $"{name}:{tenant}";
            var checkpoint = _projectorCheckpoints.GetValueOrDefault(key, ProjectionCheckpoint.Start);
            var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }
            _projectorCheckpoints[key] = checkpoint;
        }

        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }

    sealed class VerifiedEmailDirectory(Uuid owner, bool verified) : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default) =>
            ValueTask.FromResult<EmailAddressView?>(emailAddress == "creator@example.com"
                ? new EmailAddressView(owner, emailAddress, verified) : null);

        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<EmailAddressView>(
                userId == owner ? [new EmailAddressView(owner, "creator@example.com", verified)] : [], null));
    }
}
