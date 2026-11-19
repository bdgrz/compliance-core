using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AccessPolicyGenerationTests
{
    static readonly ActorAttribution Actor = new("deployment", "platform");
    static ImmutableContentRef Content() => new(Uuid.CreateVersion4(), new string('a', 64), 17, "application/json", "access-policy.v1");

    [Fact]
    public void ShouldActivateOnceGivenExactRetryAndRejectChangedIntent()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new AccessPolicyGeneration(operation);
        var scenario = new AggregateScenario<AccessPolicyGeneration>(owner);
        var content = Content();

        // Act
        var first = owner.Activate(operation, 0, 1, content, Actor);
        var retry = owner.Activate(operation, 0, 1, content, Actor);
        var changed = owner.Activate(operation, 0, 1, content with { Sha256 = new string('b', 64) }, Actor);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.IsType<AccessPolicyActivated>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(1UL, owner.Generation);
        Assert.Equal(content, owner.Content);
    }

    [Fact]
    public void ShouldReplayActivationGivenAnOlderExactGenerationHasBeenSuperseded()
    {
        // Arrange
        var firstId = Uuid.CreateVersion4();
        var firstContent = Content();
        var firstOwner = new AccessPolicyGeneration(firstId);
        var first = new AggregateScenario<AccessPolicyGeneration>(firstOwner);
        Assert.True(firstOwner.Activate(firstId, 0, 1, firstContent, Actor).IsSuccess);
        var secondId = Uuid.CreateVersion4();
        var secondContent = Content();
        var secondOwner = new AccessPolicyGeneration(secondId);
        var second = new AggregateScenario<AccessPolicyGeneration>(secondOwner).Given(first.PendingEvents.ToArray());
        Assert.True(secondOwner.Activate(secondId, 1, 2, secondContent, Actor).IsSuccess);
        var replay = new AccessPolicyGeneration(firstId);
        var replayScenario = new AggregateScenario<AccessPolicyGeneration>(replay)
            .Given(first.PendingEvents.Concat(second.PendingEvents).ToArray());

        // Act
        var retry = replay.Activate(firstId, 0, 1, firstContent, Actor);
        var changed = replay.Activate(firstId, 0, 1, secondContent, Actor);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(2UL, replay.Generation);
        Assert.Equal(secondContent, replay.Content);
        Assert.Empty(replayScenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectPolicyGenerationGivenItsPositionIsStaleOrNotIncreasing()
    {
        // Arrange
        var firstId = Uuid.CreateVersion4();
        var firstOwner = new AccessPolicyGeneration(firstId);
        var first = new AggregateScenario<AccessPolicyGeneration>(firstOwner);
        Assert.True(firstOwner.Activate(firstId, 0, 2, Content(), Actor).IsSuccess);
        var operation = Uuid.CreateVersion4();
        var owner = new AccessPolicyGeneration(operation);
        var scenario = new AggregateScenario<AccessPolicyGeneration>(owner).Given(first.PendingEvents.ToArray());

        // Act
        var stale = owner.Activate(operation, 0, 3, Content(), Actor);
        var unchanged = owner.Activate(operation, 1, 2, Content(), Actor);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.False(unchanged.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectLosingActivationGivenCompetingAppend()
    {
        // Arrange
        var winnerId = Uuid.CreateVersion4();
        var winner = new AccessPolicyGeneration(winnerId);
        var committed = new AggregateScenario<AccessPolicyGeneration>(winner);
        Assert.True(winner.Activate(winnerId, 0, 1, Content(), Actor).IsSuccess);
        var losingId = Uuid.CreateVersion4();
        var loser = new AccessPolicyGeneration(losingId);
        var scenario = new AggregateScenario<AccessPolicyGeneration>(loser).Given(committed.PendingEvents.ToArray());

        // Act
        var result = loser.Activate(losingId, 0, 2, Content(), Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRestorePolicyContentGivenANewerGenerationIsAppended()
    {
        // Arrange
        var original = Content();
        var firstId = Uuid.CreateVersion4();
        var firstOwner = new AccessPolicyGeneration(firstId);
        var first = new AggregateScenario<AccessPolicyGeneration>(firstOwner);
        Assert.True(firstOwner.Activate(firstId, 0, 1, original, Actor).IsSuccess);
        var nextId = Uuid.CreateVersion4();
        var nextOwner = new AccessPolicyGeneration(nextId);
        var next = new AggregateScenario<AccessPolicyGeneration>(nextOwner).Given(first.PendingEvents.ToArray());

        // Act
        var result = nextOwner.Activate(nextId, 1, 2, original, Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2UL, nextOwner.Generation);
        Assert.Equal(original, nextOwner.Content);
        Assert.Single(next.PendingEvents);
    }

    [Fact]
    public void ShouldRejectHistoryGivenChangedOriginalPosition()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new AccessPolicyGeneration(operation);
        var source = new AggregateScenario<AccessPolicyGeneration>(owner);
        Assert.True(owner.Activate(operation, 0, 1, Content(), Actor).IsSuccess);
        var original = Assert.IsType<AccessPolicyActivated>(Assert.Single(source.PendingEvents));
        var corrupt = original with { ExpectedPosition = 1 };

        // Act
        var replay = new AggregateScenario<AccessPolicyGeneration>(new AccessPolicyGeneration(operation));

        // Assert
        Assert.Throws<InvalidOperationException>(() => replay.Given(corrupt));
    }

    [Fact]
    public void ShouldRefusePolicyContentGivenItExceedsTheCurrentLimit()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new AccessPolicyGeneration(operation);
        var scenario = new AggregateScenario<AccessPolicyGeneration>(owner);
        var oversized = Content() with { Bytes = 16 * 1024 + 1 };

        // Act
        var result = owner.Activate(operation, 0, 1, oversized, Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0UL, owner.Generation);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldAcceptPolicyContentGivenItIsAtTheCurrentLimit()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new AccessPolicyGeneration(operation);
        var scenario = new AggregateScenario<AccessPolicyGeneration>(owner);
        var maximum = Content() with { Bytes = 16 * 1024 };

        // Act
        var result = owner.Activate(operation, 0, 1, maximum, Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(maximum, Assert.IsType<AccessPolicyActivated>(Assert.Single(scenario.PendingEvents)).Content);
    }

    [Fact]
    public void ShouldRejectReplayGivenOversizedPolicyContentReference()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new AccessPolicyGeneration(operation);
        var source = new AggregateScenario<AccessPolicyGeneration>(owner);
        Assert.True(owner.Activate(operation, 0, 1, Content(), Actor).IsSuccess);
        var fact = Assert.IsType<AccessPolicyActivated>(Assert.Single(source.PendingEvents));
        var corrupt = fact with { Content = fact.Content with { Bytes = 16 * 1024 + 1 } };

        // Act
        var replay = new AggregateScenario<AccessPolicyGeneration>(new AccessPolicyGeneration(operation));

        // Assert
        Assert.Throws<InvalidOperationException>(() => replay.Given(corrupt));
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("bytes")]
    [InlineData("schema")]
    [InlineData("large")]
    public void ShouldRejectPolicyReferenceGivenItIsInvalidOrUnbounded(string error)
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var owner = new AccessPolicyGeneration(operation);
        var scenario = new AggregateScenario<AccessPolicyGeneration>(owner);
        var content = Content();
        content = error switch
        {
            "digest" => content with { Sha256 = new string('A', 64) },
            "bytes" => content with { Bytes = 0 },
            "schema" => content with { Schema = " " },
            "large" => content with { Schema = new string('x', 4097) },
            _ => content,
        };

        // Act
        var result = owner.Activate(operation, 0, 1, content, Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0UL, owner.Generation);
        Assert.Empty(scenario.PendingEvents);
    }
}
