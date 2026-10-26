using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

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

    static OperationProvenance Operation(
        ApplicationRelationship owner,
        Uuid actorId, DateTimeOffset acceptedAt) => new(owner.Id, owner.Id, null,
        "AddApplicationRelationship", ActorReference.ForMember(actorId, "Inventory manager"),
        owner.Stream, "initial", 0, new string('a', 64), acceptedAt, owner.Stream, owner.Stream);

    static ImmutableContentRef Content(string name) => new(Uuid.CreateVersion4(),
        new string('b', 64), name.Length, "application/json", name);
}
