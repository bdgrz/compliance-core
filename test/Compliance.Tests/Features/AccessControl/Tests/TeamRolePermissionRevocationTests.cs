using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class TeamRolePermissionRevocationTests
{
    static readonly ActorAttribution Actor = new("administrator", "bdgrz");
    static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("active", true)]
    [InlineData("direct_grant", false)]
    [InlineData("deleted_team", false)]
    [InlineData("deleted_role", false)]
    [InlineData("removed_permission", false)]
    [InlineData("removed_assignment", false)]
    [InlineData("removed_member", false)]
    [InlineData("suspended_member", false)]
    [InlineData("suspended_tenant", false)]
    [InlineData("new_episode", false)]
    public async Task ShouldUseCurrentOwnersGivenUnprojectedAccessChange(string change, bool expected)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ChangeAsync(change);

        // Act
        var allowed = await fixture.Authorizer.IsAllowedAsync(fixture.Tenant, fixture.User,
            fixture.MemberId, RbacPermissions.ProgramManage);

        // Assert
        Assert.Equal(expected, allowed);
    }

    [Theory]
    [InlineData("application")]
    [InlineData("system_instance")]
    [InlineData("information_asset")]
    public async Task ShouldLimitPermissionToExactResourceGivenSharedResourceRole(string kind)
    {
        // Arrange
        var resource = Uuid.CreateVersion4();
        var grant = new AccessGrantScope(AccessGrantScopeKind.SharedResource, resource, kind);
        await using var fixture = await Fixture.CreateAsync(grant);
        var query = new AccessGrantScope(kind == "application" ? AccessGrantScopeKind.Application :
            kind == "system_instance" ? AccessGrantScopeKind.SystemInstance : AccessGrantScopeKind.SharedResource,
            resource, kind == "information_asset" ? kind : null);

        // Act
        var exact = await fixture.Authorizer.IsAllowedAtAnyScopeAsync(fixture.Tenant, fixture.User,
            fixture.MemberId, [query], RbacPermissions.ProgramManage);
        var other = await fixture.Authorizer.IsAllowedAtAnyScopeAsync(fixture.Tenant, fixture.User,
            fixture.MemberId, [query with { Id = Uuid.CreateVersion4() }], RbacPermissions.ProgramManage);
        var standing = await fixture.Authorizer.IsAllowedAsync(fixture.Tenant, fixture.User,
            fixture.MemberId, RbacPermissions.ProgramManage);

        // Assert
        Assert.True(exact);
        Assert.False(other);
        Assert.False(standing);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task ShouldUseUtcEndDateGivenGuestHasTeamPermission(int daysLater, bool expected)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(guest: true);
        var authorizer = new TeamRolePermissionAuthorizer(fixture.Reader, fixture.Events,
            new FixedClock(At.AddDays(daysLater).AddHours(11).AddMinutes(59)));

        // Act
        var allowed = await authorizer.IsAllowedAsync(fixture.Tenant, fixture.User,
            fixture.MemberId, fixture.Program, RbacPermissions.ProgramManage);

        // Assert
        Assert.Equal(expected, allowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invented.permission")]
    public async Task ShouldDenyGivenMissingOrUnknownPermission(string? permission)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var allowed = await fixture.Authorizer.IsAllowedAsync(fixture.Tenant, fixture.User,
            fixture.MemberId, permission!);

        // Assert
        Assert.False(allowed);
    }

    [Theory]
    [InlineData("deleted_team")]
    [InlineData("deleted_role")]
    [InlineData("removed_permission")]
    [InlineData("removed_assignment")]
    [InlineData("removed_member")]
    [InlineData("suspended_member")]
    public async Task ShouldDenyGivenRevocationCommitsDuringPermissionRead(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var reader = new AfterPermissionRead(fixture.Reader, () => fixture.ChangeAsync(change));
        var authorizer = new TeamRolePermissionAuthorizer(reader, fixture.Events, new FixedClock(At));

        // Act
        var allowed = await authorizer.IsAllowedAsync(fixture.Tenant, fixture.User,
            fixture.MemberId, RbacPermissions.ProgramManage);

        // Assert
        Assert.False(allowed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldExplainProgramLimitsWithoutStandingAuthorityGivenScopedMemberAccess(bool guest)
    {
        // Arrange
        var program = Uuid.CreateVersion4();
        await using var fixture = await Fixture.CreateAsync(guest ? null : new AccessGrantScope(AccessGrantScopeKind.Program, program), guest);
        var names = new DisplayNames(fixture.Team, fixture.Role);
        var handler = new GetTeamMemberAccessHandler(fixture.Reader, fixture.Authorizer, names, names);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new(fixture.Tenant, fixture.User), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var path = Assert.Single(result.Value.Paths);
        Assert.Equal(new AccessGrantScope(AccessGrantScopeKind.Program, guest ? fixture.Program : program), path.Scope);
        Assert.Contains(RbacPermissions.ProgramManage, path.Permissions);
        Assert.Empty(result.Value.EffectivePermissions);
        Assert.Empty(result.Value.GrantPaths);
    }

    [Theory]
    [InlineData("pending", false)]
    [InlineData("designated", true)]
    [InlineData("released", false)]
    public async Task ShouldUseDesignationWithoutTeamsGivenPrimaryAdministrator(string state, bool expected)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ChangeAsync("removed_assignment");
        var designation = await fixture.SeedPrimaryAsync(state);
        var names = new DisplayNames(fixture.Team, fixture.Role);
        var handler = new GetTeamMemberAccessHandler(fixture.Reader, fixture.Authorizer, names, names);
        // Act
        var allowed = await fixture.Authorizer.IsAllowedAsOrganizationRoleAsync(fixture.Tenant,
            fixture.User, fixture.MemberId, BuiltInRbac.TenantAdministrationRoleId(fixture.Tenant),
            RbacPermissions.TenantRbacManage);
        var explanation = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new(fixture.Tenant, fixture.User), RequestActor.System), CancellationToken.None);
        // Assert
        Assert.Equal(expected, allowed);
        Assert.True(explanation.IsSuccess, explanation.Error?.Message);
        Assert.Equal(expected ? designation : Uuid.Empty, explanation.Value.PrimaryDesignationId);
        Assert.Equal(expected, explanation.Value.EffectivePermissions.Contains(RbacPermissions.TenantRbacManage));
        Assert.Empty(explanation.Value.Paths);
        Assert.Empty(explanation.Value.GrantPaths);
    }

    sealed class DisplayNames(Uuid team, Uuid role) : ITeamDirectoryReader, IRoleDirectoryReader
    {
        ValueTask<TeamView?> ITeamDirectoryReader.GetAsync(Uuid tenantId, Uuid teamId, CancellationToken ct) =>
            ValueTask.FromResult<TeamView?>(teamId == team ? new(team, "Reviewers") : null);

        ValueTask<RoleView?> IRoleDirectoryReader.GetAsync(Uuid tenantId, Uuid roleId, CancellationToken ct) =>
            ValueTask.FromResult<RoleView?>(roleId == role ? new(role, "Custom role") : null);

        ValueTask<Page<TeamView>> ITeamDirectoryReader.ListAsync(Uuid tenantId, int? limit,
            string? cursor, string? search, bool descending, CancellationToken ct) => throw new NotSupportedException();

        ValueTask<Page<RoleView>> IRoleDirectoryReader.ListAsync(Uuid tenantId, int? limit,
            string? cursor, string? search, bool descending, CancellationToken ct) => throw new NotSupportedException();
    }

    sealed class AfterPermissionRead(IAggregateReader inner, Func<Task> after) : IAggregateReader
    {
        bool _changed;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            var owner = await inner.HydrateAsync(aggregate, ct);
            if (!_changed && owner is RolePermission)
            {
                _changed = true;
                await after();
            }
            return owner;
        }
    }

    sealed class Fixture(ServiceProvider provider) : IAsyncDisposable
    {
        readonly AsyncServiceScope _scope = provider.CreateAsyncScope();
        ServiceProvider Provider => provider;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Team { get; } = Uuid.CreateVersion4();
        public Uuid Role { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid MemberId => RbacIds.Member(Tenant, User);
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IDomainEventReader Events => provider.GetRequiredService<IDomainEventReader>();
        public TeamRolePermissionAuthorizer Authorizer => new(Reader, Events, new FixedClock(At));
        Uuid Assignment { get; } = Uuid.CreateVersion4();
        Uuid PermissionAssignment { get; } = Uuid.CreateVersion4();
        AccessGrantScope Scope { get; set; } = null!;

        public static async Task<Fixture> CreateAsync(AccessGrantScope? assignmentScope = null, bool guest = false)
        {
            var fixture = new Fixture(RbacManagementServices.Build(true, portia =>
                portia.AddRequestHandler<Bdgrz.Compliance.Features.AccessControl.Handlers.RegisterMemberHandler>()));
            fixture.Scope = assignmentScope ?? new AccessGrantScope(AccessGrantScopeKind.Organization, fixture.Tenant);
            await fixture.SeedAsync(new Tenant(fixture.Tenant), owner =>
            {
                Assert.True(owner.Register(fixture.User, "Acme", "scope-test").IsSuccess);
                return owner.ConfirmSlug("scope-test");
            });
            await using (var scope = fixture.Provider.CreateAsyncScope())
            {
                var registered = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
                    new Bdgrz.Compliance.Features.AccessControl.Commands.RegisterMember(Uuid.CreateVersion4(),
                        fixture.Tenant, fixture.User, guest ? "guest" : "client_personnel",
                        guest ? DateOnly.FromDateTime(At.UtcDateTime) : null, guest ? [fixture.Program] : null), RequestActor.System);
                Assert.True(registered.IsSuccess, registered.Error?.Message);
            }
            var member = await fixture.Reader.HydrateAsync(new Member(fixture.Tenant, fixture.User));
            await fixture.SeedAsync(new Team(fixture.Tenant, fixture.Team), owner => owner.Define("Reviewers"));
            await fixture.SeedAsync(new Role(fixture.Tenant, fixture.Role), owner => owner.Define("Custom role"));
            await fixture.SeedAsync(new TeamMember(fixture.Tenant, fixture.Team, fixture.MemberId), owner => owner.Assign(member.MembershipEpisodeId));
            await fixture.SeedAsync(new TeamRole(fixture.Tenant, fixture.Team, fixture.Role, fixture.Assignment, fixture.Scope),
                owner => owner.Assign(fixture.Assignment, 0, Actor));
            await fixture.SeedAsync(new RolePermission(fixture.Tenant, fixture.Role, RbacPermissions.ProgramManage, fixture.PermissionAssignment),
                owner => owner.Assign(fixture.PermissionAssignment, 0, Actor));
            return fixture;
        }

        public async Task<Uuid> SeedPrimaryAsync(string state)
        {
            var member = await Reader.HydrateAsync(new Member(Tenant, User));
            var designation = Uuid.CreateVersion4();
            await SeedAsync(new PrimaryOrgAdmins(Tenant, designation), owner => owner.Designate(designation,
                User, member.MembershipEpisodeId));
            if (state == "pending")
                return designation;
            await SeedAsync(new Member(Tenant, User), owner => owner.MarkPrimary(designation, member.MembershipEpisodeId));
            await SeedAsync(new PrimaryOrgAdmins(Tenant), owner => owner.RecordOutcome(designation,
                User, member.MembershipEpisodeId, null));
            if (state == "released")
            {
                var replacement = Uuid.CreateVersion4();
                var replacementId = Uuid.CreateVersion4();
                await SeedAsync(new PrimaryOrgAdmins(Tenant, replacementId), owner => owner.Designate(replacementId,
                    replacement, member.MembershipEpisodeId));
                await SeedAsync(new PrimaryOrgAdmins(Tenant), owner => owner.RecordOutcome(replacementId,
                    replacement, member.MembershipEpisodeId, null));
                var release = Uuid.CreateVersion4();
                await SeedAsync(new PrimaryOrgAdmins(Tenant, release), owner => owner.Release(release, User,
                    designation, "Replacement"));
                // Leave the original mark in place: releasing the seat revokes authority before delayed cleanup.
            }
            return designation;
        }

        public async Task ChangeAsync(string change)
        {
            switch (change)
            {
                case "deleted_team":
                    await SeedAsync(new Team(Tenant, Team), owner => owner.Delete());
                    break;
                case "deleted_role":
                    await SeedAsync(new Role(Tenant, Role), owner => owner.Delete());
                    break;
                case "removed_permission":
                    var removePermission = Uuid.CreateVersion4();
                    await SeedAsync(new RolePermission(Tenant, Role, RbacPermissions.ProgramManage, removePermission),
                        owner => owner.Remove(removePermission, 1, PermissionAssignment, Actor));
                    break;
                case "direct_grant":
                    await ChangeAsync("removed_assignment");
                    var member = await Reader.HydrateAsync(new Member(Tenant, User));
                    await SeedAsync(new AccessGrant(Tenant, Uuid.CreateVersion4()), owner => owner.Issue(
                        new AccessGrantTerms(new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId), Role,
                            new AccessGrantScope(AccessGrantScopeKind.Organization, Tenant), new AccessGrantSource("manual", "legacy"),
                            ActorReference.ForMember(Uuid.CreateVersion4(), "Admin"), At, null), member.MembershipEpisodeId));
                    break;
                case "removed_assignment":
                    var removeAssignment = Uuid.CreateVersion4();
                    await SeedAsync(new TeamRole(Tenant, Team, Role, removeAssignment, Scope),
                        owner => owner.Remove(removeAssignment, 1, Assignment, Actor));
                    break;
                case "removed_member":
                    await SeedAsync(new TeamMember(Tenant, Team, MemberId), owner => owner.Remove());
                    break;
                case "suspended_member":
                    await SeedAsync(new Member(Tenant, User), owner => owner.Suspend(Uuid.CreateVersion4(), "Admin", At, "Suspended"));
                    break;
                case "suspended_tenant":
                    await SeedAsync(new Tenant(Tenant), owner => owner.Suspend(Uuid.CreateVersion4(), "Suspended", At));
                    break;
                case "new_episode":
                    await SeedAsync(new Member(Tenant, User), owner => owner.Deprovision(Uuid.CreateVersion4(), "Admin", At, "Ended"));
                    await SeedAsync(new Member(Tenant, User), owner => owner.CompleteDeprovisionCleanup(owner.TerminationProof!,
                        MemberRetirementEffects.CompletionId(owner.TerminationProof!.EventId, owner.Id, owner.MembershipEpisodeId), At));
                    await SeedAsync(new Member(Tenant, User), owner => owner.Register());
                    break;
            }
        }

        Task SeedAsync<T>(T owner, Func<T, Result> operation) where T : Aggregate =>
            RbacManagementServices.SeedAsync(provider, owner, operation);

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
