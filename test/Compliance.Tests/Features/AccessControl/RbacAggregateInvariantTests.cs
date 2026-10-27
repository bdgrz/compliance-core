using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RbacAggregateInvariantTests
{
    [Fact]
    public void ShouldRejectRenameGivenUndefinedCustomRole()
    {
        // Arrange
        var role = new Role(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var scenario = new AggregateScenario<Role>(role);

        // Act
        var result = role.Rename("Reviewer");

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRejectDefinitionGivenDifferentCreationIdentity(bool team)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var role = new Role(tenant, id);
        var group = new Team(tenant, id);

        // Act
        var result = team
            ? group.Define(RbacFacts.Provenance(group, "DefineTeam", command), TenantFacts.Response(), "Reviewers")
            : role.Define(RbacFacts.Provenance(role, "DefineRole", command), TenantFacts.Response(), "Reviewer");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(team ? group.IsActive : role.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRejectForeignDefinitionGivenReplay(bool team)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var foreign = Uuid.CreateVersion4();
        var role = new Role(tenant, id);
        var group = new Team(tenant, id);
        var roleFact = RbacFacts.RoleDefinedFact(tenant, id, "Reviewer") with { TenantId = foreign };
        var teamFact = RbacFacts.TeamDefinedFact(tenant, id, "Reviewers") with { TenantId = foreign };

        // Act
        var error = Record.Exception(() =>
        {
            if (team)
                new AggregateScenario<Team>(group).Given(DomainEventSeed.Attach(teamFact, id, 1));
            else
                new AggregateScenario<Role>(role).Given(DomainEventSeed.Attach(roleFact, id, 1));
        });

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRejectRedefinitionGivenDeletedOwnerOnReplay(bool team)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var role = new Role(tenant, id);
        var group = new Team(tenant, id);

        // Act
        var error = Record.Exception(() =>
        {
            if (team)
                new AggregateScenario<Team>(group).Given(DomainEventSeed.Attach(RbacFacts.TeamDefinedFact(tenant, id, "First"), id, 1),
                    DomainEventSeed.Attach(RbacFacts.TeamDeletedFact(tenant, id), id, 2),
                    DomainEventSeed.Attach(RbacFacts.TeamDefinedFact(tenant, id, "Second"), id, 3));
            else
                new AggregateScenario<Role>(role).Given(DomainEventSeed.Attach(RbacFacts.RoleDefinedFact(tenant, id, "First"), id, 1),
                    DomainEventSeed.Attach(RbacFacts.RoleDeletedFact(tenant, id), id, 2),
                    DomainEventSeed.Attach(RbacFacts.RoleDefinedFact(tenant, id, "Second"), id, 3));
        });

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRejectDeletionGivenBuiltInOwnerOnReplay(bool team)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = team ? BuiltInRbac.AdministratorsTeamId(tenant) : BuiltInRbac.ViewerRoleId(tenant);
        var role = new Role(tenant, id);
        var group = new Team(tenant, id);

        // Act
        var error = Record.Exception(() =>
        {
            if (team)
                new AggregateScenario<Team>(group).Given(DomainEventSeed.Attach(RbacFacts.TeamDefinedFact(tenant, id, "Built in"), id, 1),
                    DomainEventSeed.Attach(RbacFacts.TeamDeletedFact(tenant, id), id, 2));
            else
                new AggregateScenario<Role>(role).Given(DomainEventSeed.Attach(RbacFacts.RoleDefinedFact(tenant, id, "Built in"), id, 1),
                    DomainEventSeed.Attach(RbacFacts.RoleDeletedFact(tenant, id), id, 2));
        });

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void ShouldRejectAssignmentGivenEmptyMembershipEpisodeOnReplay()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var owner = new TeamMember(tenant, team, member);
        var fact = TeamMemberFacts.Assigned(tenant, team, member) with { MembershipEpisodeId = Uuid.Empty };

        // Act
        var error = Record.Exception(() => new AggregateScenario<TeamMember>(owner).Given(DomainEventSeed.Attach(fact, owner.Id, 1)));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }
}
