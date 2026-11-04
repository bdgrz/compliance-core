using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class OrganizationRoleAuthorityTests
{
    [Theory]
    [InlineData("organization", true)]
    [InlineData("program", false)]
    [InlineData("removed", false)]
    [InlineData("deleted_team", false)]
    [InlineData("inactive_member", false)]
    [InlineData("other_role", false)]
    public async Task ShouldUseCurrentOrganizationTeamRoleOwnersGivenRoleRestrictedAction(string condition, bool expected)
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(true, static _ => { });
        await using var scope = provider.CreateAsyncScope();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenant, user);
        var teamId = Uuid.CreateVersion4();
        var roleId = condition == "other_role" ? Uuid.CreateVersion4() : BuiltInRbac.ComplianceManagementRoleId(tenant);
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        await RbacManagementServices.SeedAsync(provider, new Member(tenant, user), member => member.Register());
        var member = await reader.HydrateAsync(new Member(tenant, user));
        await RbacManagementServices.SeedAsync(provider, new Team(tenant, teamId), team => team.Define("Reviewers"));
        await RbacManagementServices.SeedAsync(provider, new Role(tenant, roleId), role => role.Define("Compliance Lead"));
        await RbacManagementServices.SeedAsync(provider, new TeamMember(tenant, teamId, memberId),
            assignment => assignment.Assign(member.MembershipEpisodeId));
        var assignmentId = Uuid.CreateVersion4();
        var assignmentScope = new AccessGrantScope(condition == "program" ? AccessGrantScopeKind.Program : AccessGrantScopeKind.Organization,
            condition == "program" ? Uuid.CreateVersion4() : tenant);
        await RbacManagementServices.SeedAsync(provider, new TeamRole(tenant, teamId, roleId, assignmentId, assignmentScope),
            assignment => assignment.Assign(assignmentId, 0, new ActorAttribution("setup", "Portia")));
        var permissionId = Uuid.CreateVersion4();
        await RbacManagementServices.SeedAsync(provider,
            new RolePermission(tenant, roleId, RbacPermissions.ProgramManage, permissionId),
            permission => permission.Assign(permissionId, 0, new ActorAttribution("setup", "Portia"), builtInSetup: true));
        if (condition == "deleted_team")
            await RbacManagementServices.SeedAsync(provider, new Team(tenant, teamId), team => team.Delete());
        if (condition == "removed")
        {
            var removal = Uuid.CreateVersion4();
            await RbacManagementServices.SeedAsync(provider, new TeamRole(tenant, teamId, roleId, removal, assignmentScope),
                assignment => assignment.Remove(removal, 1, assignmentId, new ActorAttribution("setup", "Portia")));
        }
        var eligibility = new EventSourcedMemberAccessEligibility(reader);
        var authorizer = new AccessGrantPermissionAuthorizer(null!, new FixedMembershipDirectory(condition != "inactive_member"),
            null!, null!, reader, TimeProvider.System, eligibility, scope.ServiceProvider.GetRequiredService<IDomainEventReader>());

        // Act
        var allowed = await authorizer.IsAllowedAsOrganizationRoleAsync(tenant, user, memberId,
            BuiltInRbac.ComplianceManagementRoleId(tenant), RbacPermissions.ProgramManage);

        // Assert
        Assert.Equal(expected, allowed);
    }
}
