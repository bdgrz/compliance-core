using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ScopedTeamRoleTests
{
    static readonly ActorAttribution Actor = new("administrator", "bdgrz");

    [Fact]
    public void ShouldGiveDifferentOwnersGivenDifferentAssignmentScopes()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var first = new TeamRole(tenant, team, role, command, new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()));
        var second = new TeamRole(tenant, team, role, command, new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()));

        // Act
        var acceptedFirst = first.Assign(command, 0, Actor);
        var acceptedSecond = second.Assign(command, 0, Actor);

        // Assert
        Assert.True(acceptedFirst.IsSuccess);
        Assert.True(acceptedSecond.IsSuccess);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Stream, second.Stream);
        Assert.NotEqual(first.Scope, second.Scope);
    }

    [Theory]
    [InlineData("program_admin")]
    [InlineData("foreign_organization")]
    [InlineData("application")]
    [InlineData("advisor")]
    [InlineData("attest")]
    public void ShouldRefuseWithoutFactGivenInvalidRoleAssignmentScope(string invalid)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var role = invalid switch
        {
            "program_admin" => BuiltInRbac.TenantAdministrationRoleId(tenant),
            "advisor" => BuiltInRbac.AdvisorRoleId(tenant),
            "attest" => BuiltInRbac.AttestRoleId(tenant),
            _ => Uuid.CreateVersion4(),
        };
        var kind = invalid == "foreign_organization" ? AccessGrantScopeKind.Organization :
            invalid == "application" ? AccessGrantScopeKind.Application : AccessGrantScopeKind.Program;
        var owner = new TeamRole(tenant, Uuid.CreateVersion4(), role, command, new AccessGrantScope(kind, Uuid.CreateVersion4()));

        // Act
        var result = owner.Assign(command, 0, Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(owner.IsAssigned);
        Assert.Empty(new AggregateScenario<TeamRole>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseProtectedRemovalGivenDirectOwnerCall()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var owner = new TeamRole(tenant, BuiltInRbac.AdministratorsTeamId(tenant),
            BuiltInRbac.TenantAdministrationRoleId(tenant), command);
        Assert.True(owner.Assign(command, 0, Actor).IsSuccess);

        // Act
        var result = owner.Remove(command, 1, command, Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(owner.IsAssigned);
        Assert.Single(new AggregateScenario<TeamRole>(owner).PendingEvents);
    }
}
