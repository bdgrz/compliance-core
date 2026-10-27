using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

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
