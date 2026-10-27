using System.Globalization;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class TeamRoleTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid TeamId = Uuid.Parse("2cc8e854-a49a-42a1-9581-d0622de3d5c3", CultureInfo.InvariantCulture);
    static readonly Uuid RoleId = BuiltInRbac.ComplianceManagementRoleId(TenantId);
    static readonly ActorAttribution Actor = new("portia:system", "Portia");

    [Fact]
    public async Task ShouldNotRestoreAuthorityGivenAnExactRetryAfterTeamRoleRemoval()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var identity = RequestMetadata.Create();
        var assignment = new AssignTeamRole(TenantId, TeamId, RoleId, identity.RequestId, 0);
        Assert.True((await bus.DispatchAsync(assignment, bus.CreateContext(RequestActor.System, identity))).IsSuccess);
        Assert.True((await bus.SendAsync(new RemoveTeamRole(TenantId, TeamId, RoleId,
            Uuid.CreateVersion4(), 1, identity.RequestId), RequestActor.System)).IsSuccess);

        // Act
        var result = await bus.DispatchAsync(assignment, bus.CreateContext(RequestActor.System, identity));
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamRole(TenantId, TeamId, RoleId));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(current.IsAssigned);
        Assert.Equal(2UL, current.Version);
    }

    [Fact]
    public void ShouldAssignOnceGivenAnExactPendingRetry()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var scenario = Given(id);

        // Act
        var first = scenario.Aggregate.Assign(id, 0, Actor);
        var retry = scenario.Aggregate.Assign(id, 0, Actor);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(id, scenario.Aggregate.AssignmentId);
        Assert.IsType<TeamRoleAssigned>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldRetainNewAuthorityGivenAnExactOldRemovalRetry()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        var scenario = Given(removal, Assigned(first), Removed(removal, first), Assigned(next, 2));

        // Act
        var result = scenario.Aggregate.Remove(removal, 1, first, Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(next, scenario.Aggregate.AssignmentId);
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("kind")]
    public void ShouldConflictGivenChangedIntentUnderAnAcceptedAssignmentIdentity(string change)
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var scenario = Given(first, Assigned(first), Removed(Uuid.CreateVersion4(), first), Assigned(Uuid.CreateVersion4(), 2));

        // Act
        var result = change == "kind" ? scenario.Aggregate.Remove(first, 0, first, Actor) :
            scenario.Aggregate.Assign(first, change == "revision" ? 1UL : 0,
                change == "actor" ? new ActorAttribution("another-system", "Portia") : Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("episode")]
    [InlineData("kind")]
    public void ShouldConflictGivenChangedIntentUnderAnAcceptedRemovalIdentity(string change)
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var scenario = Given(removal, Assigned(first), Removed(removal, first), Assigned(Uuid.CreateVersion4(), 2));

        // Act
        var result = change == "kind" ? scenario.Aggregate.Assign(removal, 1, Actor) :
            scenario.Aggregate.Remove(removal, change == "revision" ? 3UL : 1,
                change == "episode" ? Uuid.CreateVersion4() : first,
                change == "actor" ? new ActorAttribution("another-system", "Portia") : Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldPreserveNewAuthorityGivenDelayedRemovalOfAnOldEpisodeAtTheCurrentRevision()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var scenario = Given(removal, Assigned(first), Removed(Uuid.CreateVersion4(), first), Assigned(next, 2));

        // Act
        var result = scenario.Aggregate.Remove(removal, 3, first, Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(next, scenario.Aggregate.AssignmentId);
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void ShouldRequireNewIntentAndRevisionGivenAssignmentAfterRemoval(int expectedRevision, bool succeeds)
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        var scenario = Given(next, Assigned(first), Removed(Uuid.CreateVersion4(), first));

        // Act
        var result = scenario.Aggregate.Assign(next, (ulong)expectedRevision, Actor);

        // Assert
        Assert.Equal(succeeds, result.IsSuccess);
        Assert.Equal(succeeds ? next : (Uuid?)null, scenario.Aggregate.AssignmentId);
        Assert.Equal(succeeds ? 1 : 0, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRejectDistinctIntentGivenAnExistingActiveAssignment()
    {
        // Arrange
        var next = Uuid.CreateVersion4();
        var scenario = Given(next, Assigned(Uuid.CreateVersion4()));

        // Act
        var result = scenario.Aggregate.Assign(next, 1, Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldConflictGivenAnotherAuthorizedMemberRetriesAcceptedIntent()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var command = new AssignTeamRole(TenantId, TeamId, RoleId, Uuid.CreateVersion4(), 0);
        Assert.True((await bus.SendAsync(command, RbacManagementServices.Actor())).IsSuccess);

        // Act
        var result = await bus.SendAsync(command, RbacManagementServices.Actor());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamRole(TenantId, TeamId, RoleId));
        Assert.Equal(1UL, owner.Version);
    }

    [Fact]
    public async Task ShouldAuthorizeBeforeRetryResolutionGivenAnActorWithoutCurrentPermission()
    {
        // Arrange
        await using var provider = Services(allowed: false);
        await using var scope = provider.CreateAsyncScope();
        var command = new AssignTeamRole(TenantId, TeamId, RoleId, Uuid.CreateVersion4(), 0);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(command, RequestActor.System)).IsSuccess);

        // Act
        // Assert
        await RequestScenario.For(provider).GivenActor(RbacManagementServices.Actor()).When(command)
            .ExpectDenied(RequestErrorKind.Forbidden).ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldPreserveTheAdministratorRoleLinkGivenRemovalIntent()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var team = BuiltInRbac.AdministratorsTeamId(TenantId);
        var role = BuiltInRbac.TenantAdministrationRoleId(TenantId);
        var assignment = new AssignTeamRole(TenantId, team, role, Uuid.CreateVersion4(), 0);
        Assert.True((await bus.SendAsync(assignment, RequestActor.System)).IsSuccess);

        // Act
        var result = await bus.SendAsync(new RemoveTeamRole(TenantId, team, role,
            Uuid.CreateVersion4(), 1, assignment.CommandId), RequestActor.System);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamRole(TenantId, team, role));
        Assert.Equal(assignment.CommandId, owner.AssignmentId);
        Assert.Equal(1UL, owner.Version);
    }

    [Fact]
    public async Task ShouldRejectAnEmptyTypedIdentityGivenAnAuthorizedCommand()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new AssignTeamRole(TenantId, TeamId, RoleId, Uuid.Empty, 0), RequestActor.System);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldPreserveSelectedNativeIntentGivenConcreteDurableRoundTrips()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var first = Uuid.CreateVersion4();
        var owner = new TeamRole(TenantId, TeamId, RoleId, first);
        var facts = new DomainEvent[] { Assigned(first), Removed(Uuid.CreateVersion4(), first), Assigned(Uuid.CreateVersion4(), 2) };
        var restored = facts.Select((fact, index) => serializer.Deserialize(serializer.Serialize(
            DomainEventSeed.Attach(fact, owner.Id, (ulong)index + 1)))).ToArray();
        var scenario = new AggregateScenario<TeamRole>(owner).Given(restored);

        // Act
        var result = owner.Assign(first, 0, Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3UL, owner.Version);
        Assert.NotEqual(first, owner.AssignmentId);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(Actor, Assert.IsType<TeamRoleAssigned>(restored[0]).Actor);
        Assert.Equal(first, Assert.IsType<TeamRoleRemoved>(restored[1]).AssignmentId);
    }

    static ServiceProvider Services(bool allowed = true) => RbacManagementServices.Build(allowed,
        portia => portia.AddRequestHandler<AssignTeamRoleHandler>().AddRequestHandler<RemoveTeamRoleHandler>());
    static TeamRoleAssigned Assigned(Uuid id, ulong revision = 0) => new(TenantId, TeamId, RoleId, id, revision, Actor);
    static TeamRoleRemoved Removed(Uuid id, Uuid assignment, ulong revision = 1) => new(TenantId, TeamId, RoleId, id, revision, Actor, assignment);
    static AggregateScenario<TeamRole> Given(Uuid commandId, params DomainEvent[] facts)
    {
        var owner = new TeamRole(TenantId, TeamId, RoleId, commandId);
        return new AggregateScenario<TeamRole>(owner).Given(facts.Select((fact, index) =>
            DomainEventSeed.Attach(fact, owner.Id, (ulong)index + 1)).ToArray());
    }
}
