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

public sealed class RolePermissionTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid RoleId = Uuid.Parse("c1b1b99d-2c52-46c6-ad58-55fe90bf53b3", CultureInfo.InvariantCulture);
    static readonly ActorAttribution Actor = new("portia:system", "Portia");
    const string Permission = "controls.read";

    [Fact]
    public async Task ShouldNotRestoreAuthorityGivenAnExactRetryAfterPermissionRemoval()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var identity = RequestMetadata.Create();
        var assignment = new AssignRolePermission(TenantId, RoleId, Permission, identity.RequestId, 0);
        Assert.True((await bus.DispatchAsync(assignment, bus.CreateContext(RequestActor.System, identity))).IsSuccess);
        Assert.True((await bus.SendAsync(new RemoveRolePermission(TenantId, RoleId, Permission,
            Uuid.CreateVersion4(), 1, identity.RequestId), RequestActor.System)).IsSuccess);

        // Act
        var retry = await bus.DispatchAsync(assignment, bus.CreateContext(RequestActor.System, identity));
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RolePermission(TenantId, RoleId, Permission));

        // Assert
        Assert.True(retry.IsSuccess);
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
        Assert.IsType<RolePermissionAssigned>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldRetainTheNewEpisodeGivenAnExactOldRemovalRetry()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        var scenario = Given(removal, Assigned(first), Removed(removal, first), Assigned(next, 2));

        // Act
        var retry = scenario.Aggregate.Remove(removal, 1, first, Actor);

        // Assert
        Assert.True(retry.IsSuccess);
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
        var scenario = Given(first, Assigned(first), Removed(Uuid.CreateVersion4(), first),
            Assigned(Uuid.CreateVersion4(), 2));

        // Act
        var result = change == "kind"
            ? scenario.Aggregate.Remove(first, 0, first, Actor)
            : scenario.Aggregate.Assign(first, change == "revision" ? 1UL : 0,
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
    public void ShouldCreateANewEpisodeGivenDistinctAssignmentIntentAfterRemoval()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        var scenario = Given(next, Assigned(first), Removed(Uuid.CreateVersion4(), first));

        // Act
        var result = scenario.Aggregate.Assign(next, 2, Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(next, scenario.Aggregate.AssignmentId);
        var accepted = Assert.IsType<RolePermissionAssigned>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(2UL, accepted.ExpectedRevision);
        Assert.Equal(Actor, accepted.Actor);
    }

    [Fact]
    public void ShouldRejectAStaleNewAssignmentGivenLaterRemoval()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        var scenario = Given(next, Assigned(first), Removed(Uuid.CreateVersion4(), first));

        // Act
        var result = scenario.Aggregate.Assign(next, 0, Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.False(scenario.Aggregate.IsAssigned);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectANewAssignmentGivenAnExistingActiveEpisode()
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
    public void ShouldPreserveNewAuthorityGivenRemovalTargetingAnOldEpisodeAtTheCurrentRevision()
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

    [Fact]
    public void ShouldRecognizeTheSameOwnerGivenCanonicalPermissionInput()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var owner = new RolePermission(TenantId, RoleId, " CONTROLS.READ ", id);
        var scenario = new AggregateScenario<RolePermission>(owner)
            .Given(DomainEventSeed.Attach(Assigned(id), owner.Id, 1));

        // Act
        var result = scenario.Aggregate.Assign(id, 0, Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldConflictGivenAnotherAuthorizedActorRetriesAcceptedIntent()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var request = new AssignRolePermission(TenantId, RoleId, Permission, Uuid.CreateVersion4(), 0);
        Assert.True((await bus.SendAsync(request, RequestActor.System)).IsSuccess);

        // Act
        var result = await bus.SendAsync(request, RequestActor.CreateSystem("another-system"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldDenyBeforeRetryResolutionGivenAnUnauthorizedActor()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var request = new AssignRolePermission(TenantId, RoleId, Permission, Uuid.CreateVersion4(), 0);
        Assert.True((await bus.SendAsync(request, RequestActor.System)).IsSuccess);

        // Act
        // Assert
        await RequestScenario.For(provider).GivenActor(RbacManagementServices.Actor()).When(request)
            .ExpectDenied(RequestErrorKind.Forbidden).ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldValidateTheTypedKeyGivenAnEmptyCommandIdentity()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var result = await bus.SendAsync(new AssignRolePermission(TenantId, RoleId, Permission, Uuid.Empty, 0),
            RequestActor.System);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Theory]
    [InlineData(" TENANT.ACCESS ")]
    [InlineData(" TENANT.RBAC.MANAGE ")]
    public async Task ShouldPreserveCoreAdministratorAuthorityGivenCanonicalizedRemoval(string permission)
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var roleId = BuiltInRbac.TenantAdministrationRoleId(TenantId);
        var assignment = new AssignRolePermission(TenantId, roleId, permission, Uuid.CreateVersion4(), 0);
        Assert.True((await bus.SendAsync(assignment, RequestActor.System)).IsSuccess);

        // Act
        var result = await bus.SendAsync(new RemoveRolePermission(TenantId, roleId, permission,
            Uuid.CreateVersion4(), 1, assignment.CommandId), RequestActor.System);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RolePermission(TenantId, roleId, permission));
        Assert.Equal(assignment.CommandId, owner.AssignmentId);
        Assert.Equal(1UL, owner.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldRejectInvalidPermissionInputGivenAnAuthorizedCommand(string? permission)
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var result = await bus.SendAsync(new AssignRolePermission(TenantId, RoleId, permission!,
            Uuid.CreateVersion4(), 0), RequestActor.System);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRecognizeNativeAcceptedIntentGivenConcreteDurableRoundTrips()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var assignment = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var owner = new RolePermission(TenantId, RoleId, Permission, assignment);
        var facts = new DomainEvent[] { Assigned(assignment), Removed(removal, assignment), Assigned(Uuid.CreateVersion4(), 2) };
        var restored = facts.Select((fact, index) => serializer.Deserialize(serializer.Serialize(
            DomainEventSeed.Attach(fact, owner.Id, (ulong)index + 1)))).ToArray();
        var scenario = new AggregateScenario<RolePermission>(owner).Given(restored);

        // Act
        var result = owner.Assign(assignment, 0, Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3UL, owner.Version);
        Assert.NotEqual(assignment, owner.AssignmentId);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(Actor, Assert.IsType<RolePermissionAssigned>(restored[0]).Actor);
        Assert.Equal(assignment, Assert.IsType<RolePermissionRemoved>(restored[1]).AssignmentId);
    }

    static ServiceProvider Services() => RbacManagementServices.Build(allowed: true,
        portia => portia.AddRequestHandler<AssignRolePermissionHandler>()
            .AddRequestHandler<RemoveRolePermissionHandler>());

    static RolePermissionAssigned Assigned(Uuid id, ulong revision = 0) =>
        new(TenantId, RoleId, Permission, id, revision, Actor);

    static RolePermissionRemoved Removed(Uuid id, Uuid assignment, ulong revision = 1) =>
        new(TenantId, RoleId, Permission, id, revision, Actor, assignment);

    static AggregateScenario<RolePermission> Given(Uuid commandId, params DomainEvent[] facts)
    {
        var owner = new RolePermission(TenantId, RoleId, Permission, commandId);
        return new AggregateScenario<RolePermission>(owner).Given(facts.Select((fact, index) =>
            DomainEventSeed.Attach(fact, owner.Id, (ulong)index + 1)).ToArray());
    }
}
