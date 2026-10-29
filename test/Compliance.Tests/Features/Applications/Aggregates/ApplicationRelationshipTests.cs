using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Tests.Features.Applications.Aggregates;

public sealed class ApplicationRelationshipTests
{
    [Fact]
    public void ShouldRejectNonCanonicalIdentityGivenRelationshipTuple()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var relationshipId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var owner = new ApplicationRelationship(
            tenantId, relationshipId, relationshipId);
        var operation = Operation(owner, actorId, now);

        // Act
        var result = owner.Add(operation, 0, sourceId, targetId, "depends_on", actorId,
            "Inventory manager", now, Content("first-response"), Content("result"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<ApplicationRelationship>(
            owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Theory]
    [InlineData("depends_on", "active")]
    [InlineData("replaces", "proposed")]
    public void ShouldAppendCanonicalRelationshipGivenValidRootAddition(string relationshipType,
        string expectedStatus)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var relationshipId = Uuid.CreateVersion5(sourceId,
            $"application-relationship:{relationshipType}:{targetId}");
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var owner = new ApplicationRelationship(
            tenantId, relationshipId, relationshipId);
        var operation = Operation(owner, actorId, now);

        // Act
        var result = owner.Add(operation, 0, sourceId, targetId, relationshipType, actorId,
            "Inventory manager", now, Content("first-response"), Content("result"));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(relationshipId, owner.Id);
        Assert.Equal("application-relationships", owner.Stream.Area);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(expectedStatus, owner.Status);
        var added = Assert.IsType<ApplicationRelationshipAdded>(
            Assert.Single(new AggregateScenario<ApplicationRelationship>(
                owner).PendingEvents));
        Assert.Equal(sourceId, added.ApplicationId);
        Assert.Equal(targetId, added.TargetApplicationId);
        Assert.Equal(relationshipId, added.RelationshipId);
        Assert.Equal(operation, added.Operation);
        Assert.NotNull(added.FirstResponse);
        Assert.NotNull(added.OperationResult);
    }

    [Fact]
    public void ShouldRejectSelfRelationshipGivenSameSourceAndTarget()
    {
        // Arrange
        var sourceId = Uuid.CreateVersion4();
        var relationshipId = Uuid.CreateVersion5(sourceId,
            $"application-relationship:depends_on:{sourceId}");
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var owner = new ApplicationRelationship(
            Uuid.CreateVersion4(), relationshipId, relationshipId);
        var operation = Operation(owner, actorId, now);

        // Act
        var result = owner.Add(operation, 0, sourceId, sourceId, "depends_on", actorId,
            "Inventory manager", now, Content("first-response"), Content("result"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<ApplicationRelationship>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRejectUnsupportedTypeGivenCanonicalTupleIdentity()
    {
        // Arrange
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var relationshipId = Uuid.CreateVersion5(sourceId,
            $"application-relationship:owns:{targetId}");
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var owner = new ApplicationRelationship(
            Uuid.CreateVersion4(), relationshipId, relationshipId);
        var operation = Operation(owner, actorId, now);

        // Act
        var result = owner.Add(operation, 0, sourceId, targetId, "owns", actorId,
            "Inventory manager", now, Content("first-response"), Content("result"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<ApplicationRelationship>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondAddWithoutFactGivenExistingRelationship()
    {
        // Arrange
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var relationshipId = Uuid.CreateVersion5(sourceId,
            $"application-relationship:depends_on:{targetId}");
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var owner = new ApplicationRelationship(
            Uuid.CreateVersion4(), relationshipId, relationshipId);
        var operation = Operation(owner, actorId, now);
        Assert.True(owner.Add(operation, 0, sourceId, targetId, "depends_on", actorId,
            "Inventory manager", now, Content("first-response"), Content("result")).IsSuccess);

        // Act
        var retry = owner.Add(operation, 0, sourceId, targetId, "depends_on", actorId,
            "Inventory manager", now, Content("first-response"), Content("result"));
        var stale = owner.PrepareAdd(1, sourceId, targetId, "depends_on", actorId,
            "Inventory manager", now);

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, retry.Error!.Kind);
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Single(new AggregateScenario<ApplicationRelationship>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreRelationshipStateGivenValidHistoryReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var relationshipId = Uuid.CreateVersion5(sourceId,
            $"application-relationship:replaces:{targetId}");
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var seed = new ApplicationRelationship(tenantId, relationshipId, relationshipId);
        Assert.True(seed.Add(Operation(seed, actorId, now), 0, sourceId, targetId, "replaces",
            actorId, "Inventory manager", now, Content("first-response"),
            Content("result")).IsSuccess);
        var history = NativeFacts.Raised(seed);

        // Act
        var replayed = NativeFacts.Replay(
            new ApplicationRelationship(tenantId, relationshipId, relationshipId), history);

        // Assert
        Assert.Equal(1, replayed.Revision);
        Assert.Equal("proposed", replayed.Status);
        Assert.Equal(sourceId, replayed.SourceApplicationId);
        Assert.Equal(targetId, replayed.TargetApplicationId);
        Assert.Equal("replaces", replayed.RelationshipType);
        var again = replayed.Add(Operation(replayed, actorId, now), 0, sourceId, targetId,
            "replaces", actorId, "Inventory manager", now, Content("first-response"),
            Content("result"));
        Assert.False(again.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, again.Error!.Kind);
    }

    static OperationProvenance Operation(
        ApplicationRelationship owner,
        Uuid actorId, DateTimeOffset acceptedAt) => new(owner.Id, owner.Id, null,
        "AddApplicationRelationship", ActorReference.ForMember(actorId, "Inventory manager"),
        owner.Stream, "initial", 0, new string('a', 64), acceptedAt, owner.Stream, owner.Stream);

    static ImmutableContentRef Content(string name) => new(Uuid.CreateVersion4(),
        new string('b', 64), name.Length, "application/json", name);
}
