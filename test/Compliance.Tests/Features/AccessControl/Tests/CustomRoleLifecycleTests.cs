using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class CustomRoleLifecycleTests
{
    [Fact]
    public async Task ShouldCreateRenameAndDeleteCustomRoleGivenCurrentOrgAdministrator()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(true, portia => portia
            .AddRequestHandler<DefineRoleHandler>().AddRequestHandler<RenameRoleHandler>()
            .AddRequestHandler<DeleteRoleHandler>());
        await using var scope = provider.CreateAsyncScope();
        var tenant = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        var actor = RbacManagementServices.Actor();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var define = new DefineRole(tenant, role, "Control reviewers");
        var rename = new RenameRole(Uuid.CreateVersion4(), tenant, role, "Evidence reviewers");

        // Act
        var created = await bus.SendAsync(define, actor);
        var renamed = await bus.SendAsync(rename, actor);
        var renamedRetry = await bus.SendAsync(rename, actor);
        var changed = await bus.SendAsync(rename with { Name = "Changed intent" }, actor);
        var deleted = await bus.SendAsync(new DeleteRole(Uuid.CreateVersion4(), tenant, role), actor);
        var defineRetry = await bus.SendAsync(define, actor);
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Role(tenant, role));

        // Assert
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True(renamed.IsSuccess, renamed.Error?.Message);
        Assert.True(renamedRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.True(deleted.IsSuccess);
        Assert.True(defineRetry.IsSuccess);
        Assert.False(retained.IsActive);
        Assert.Equal(3UL, retained.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseBuiltInRoleRenameWithoutFactGivenHumanOrSystemActor(bool system)
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(true, portia => portia
            .AddRequestHandler<DefineRoleHandler>().AddRequestHandler<RenameRoleHandler>());
        await using var scope = provider.CreateAsyncScope();
        var tenant = Uuid.CreateVersion4();
        var role = BuiltInRbac.ViewerRoleId(tenant);
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        Assert.True((await bus.SendAsync(new DefineRole(tenant, role, "Viewer"), RequestActor.System)).IsSuccess);

        // Act
        var result = await bus.SendAsync(new RenameRole(Uuid.CreateVersion4(), tenant, role, "Modified"),
            system ? RequestActor.System : RbacManagementServices.Actor());
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Role(tenant, role));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1UL, retained.Version);
        Assert.True(retained.IsActive);
    }

    [Fact]
    public void ShouldRefuseBuiltInRoleRenameGivenDirectOwnerInvocation()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var role = new Role(tenant, BuiltInRbac.ViewerRoleId(tenant));

        // Act
        var result = role.PrepareRename("Modified");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<Role>(role).PendingEvents);
    }

    [Fact]
    public void ShouldMaterializeCustomRolePermissionGivenActiveTeamAndRole()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, user);
        var team = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        var state = new PermissionProjectionState();
        state.Apply(MemberFacts.Registered(tenant, member, user));
        state.Apply(RbacFacts.TeamDefinedFact(tenant, team, "Reviewers"));
        state.Apply(RbacFacts.RoleDefinedFact(tenant, role, "Control reviewers"));
        state.Apply(TeamMemberFacts.Assigned(tenant, team, member));
        state.Apply(RbacTeamRoleFacts.Assigned(tenant, team, role));
        state.Apply(RbacPermissionFacts.Assigned(tenant, role, RbacPermissions.ProgramManage));

        // Act
        var grants = state.Materialize();
        var explanation = state.Explain(member);

        // Assert
        Assert.Contains(grants, grant => grant.MemberId == member && grant.Permission == RbacPermissions.ProgramManage);
        Assert.Equal(role, Assert.Single(explanation).RoleId);
    }
}
