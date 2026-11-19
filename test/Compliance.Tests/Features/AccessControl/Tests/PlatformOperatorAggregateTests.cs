using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PlatformOperatorAggregateTests
{
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly ActorAttribution Actor = new(ActorId.ToString(), "bdgrz");
    static readonly ActorAttribution SystemActor = new("portia:system", "Portia");

    [Fact]
    public void ShouldDeriveAnIndependentOwnerForEachUser()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var first = new PlatformOperator(user);

        // Act
        var same = new PlatformOperator(user);
        var other = new PlatformOperator(Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Id, same.Id);
        Assert.Equal(first.Stream, same.Stream);
        Assert.NotEqual(first.Id, other.Id);
        Assert.NotEqual(first.Stream, other.Stream);
    }

    [Fact]
    public void ShouldNeverRegrantRevokedOperatorGivenRepeatedGenesisConfiguration()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var plan = Uuid.CreateVersion4();
        var initial = new PlatformOperator(user, grant);
        var initialScenario = new AggregateScenario<PlatformOperator>(initial);
        Assert.True(initial.GrantBootstrap(grant, 0, plan, SystemActor).IsSuccess);
        var history = initialScenario.PendingEvents.ToArray();
        var revoke = Uuid.CreateVersion4();
        var revoked = new PlatformOperator(user, revoke);
        var revokeScenario = new AggregateScenario<PlatformOperator>(revoked).Given(history);
        Assert.True(revoked.Revoke(revoke, 1, grant, ActorId, Actor, "removed").IsSuccess);
        var laterOperation = Uuid.CreateVersion4();
        var later = new PlatformOperator(user, laterOperation);
        var laterScenario = new AggregateScenario<PlatformOperator>(later).Given(history.Concat(revokeScenario.PendingEvents).ToArray());

        // Act
        var result = later.GrantBootstrap(laterOperation, 2, plan, SystemActor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(later.IsActive);
        Assert.Empty(laterScenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecordOneGrantGivenExactRetryAndRejectChangedIntent()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, operation);
        var scenario = new AggregateScenario<PlatformOperator>(owner);

        // Act
        var first = owner.Grant(operation, 0, ActorId, Actor, "on call");
        var retry = owner.Grant(operation, 0, ActorId, Actor, "on call");
        var changed = owner.Grant(operation, 0, ActorId, Actor, "different");

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.IsType<PlatformOperatorAccessGranted>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(operation, owner.GrantEpisodeId);
    }

    [Fact]
    public void ShouldRefuseRevocationGivenCurrentPrimaryMark()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var marked = MarkedHistory(user, grant, lineage, 1, Uuid.CreateVersion4());
        var revoke = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, revoke);
        var scenario = new AggregateScenario<PlatformOperator>(owner).Given(marked);

        // Act
        var result = owner.Revoke(revoke, owner.Version, grant, ActorId, Actor, "rotation");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(owner.IsActive);
        Assert.True(owner.IsPrimary);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRevokeExactGrantAfterItsPrimaryMarkIsReleased()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var reservation = Uuid.CreateVersion4();
        var history = MarkedHistory(user, grant, lineage, 1, reservation);
        var release = Uuid.CreateVersion4();
        var marked = new PlatformOperator(user, release);
        var released = new AggregateScenario<PlatformOperator>(marked).Given(history);
        Assert.True(marked.RemovePrimaryMark(release, 2, grant, lineage, 1, reservation, Uuid.CreateVersion4()).IsSuccess);
        var revoke = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, revoke);
        var scenario = new AggregateScenario<PlatformOperator>(owner).Given(history.Concat(released.PendingEvents).ToArray());

        // Act
        var result = owner.Revoke(revoke, 3, grant, ActorId, Actor, "rotation");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(owner.IsActive);
        Assert.False(owner.IsPrimary);
        Assert.IsType<PlatformOperatorAccessRevoked>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldPreventLateMarkGivenTargetOwnedFence()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var reservation = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, operation);
        var scenario = new AggregateScenario<PlatformOperator>(owner).Given(GrantHistory(user, grant));

        // Act
        var fenced = owner.FencePrimaryMark(operation, 1, grant, lineage, 7, reservation);
        var retry = owner.FencePrimaryMark(operation, 1, grant, lineage, 7, reservation);
        var delayed = owner.AcceptPrimaryMark(operation, 1, grant, lineage, 7, reservation);

        // Assert
        Assert.True(fenced.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(delayed.IsSuccess);
        Assert.False(owner.IsPrimary);
        Assert.Equal(7UL, owner.HighestPrimaryGeneration);
        Assert.IsType<PlatformOperatorPrimaryMarkFenced>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldRejectFencedGenerationEvenGivenAnotherSelectedOperation()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var reservation = Uuid.CreateVersion4();
        var history = GrantHistory(user, grant);
        var fenceId = Uuid.CreateVersion4();
        var fenced = new PlatformOperator(user, fenceId);
        var fenceScenario = new AggregateScenario<PlatformOperator>(fenced).Given(history);
        Assert.True(fenced.FencePrimaryMark(fenceId, 1, grant, lineage, 7, reservation).IsSuccess);
        var attemptedId = Uuid.CreateVersion4();
        var target = new PlatformOperator(user, attemptedId);
        var scenario = new AggregateScenario<PlatformOperator>(target).Given(history.Concat(fenceScenario.PendingEvents).ToArray());

        // Act
        var result = target.AcceptPrimaryMark(attemptedId, 2, grant, lineage, 7, reservation);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(target.IsPrimary);
        Assert.Equal(7UL, target.HighestPrimaryGeneration);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldNotUnmarkNewGenerationGivenDelayedOldRelease()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var firstReservation = Uuid.CreateVersion4();
        var first = MarkedHistory(user, grant, lineage, 1, firstReservation);
        var release = Uuid.CreateVersion4();
        var released = new PlatformOperator(user, release);
        var releaseScenario = new AggregateScenario<PlatformOperator>(released).Given(first);
        Assert.True(released.RemovePrimaryMark(release, 2, grant, lineage, 1, firstReservation, Uuid.CreateVersion4()).IsSuccess);
        var afterRelease = first.Concat(releaseScenario.PendingEvents).ToArray();
        var next = Uuid.CreateVersion4();
        var replacement = new PlatformOperator(user, next);
        var nextScenario = new AggregateScenario<PlatformOperator>(replacement).Given(afterRelease);
        Assert.True(replacement.AcceptPrimaryMark(next, 3, grant, lineage, 2, Uuid.CreateVersion4()).IsSuccess);
        var history = afterRelease.Concat(nextScenario.PendingEvents).ToArray();
        var staleOperation = Uuid.CreateVersion4();
        var stale = new PlatformOperator(user, staleOperation);
        var scenario = new AggregateScenario<PlatformOperator>(stale).Given(history);

        // Act
        var result = stale.RemovePrimaryMark(staleOperation, 4, grant, lineage, 1,
            firstReservation, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(stale.IsPrimary);
        Assert.Equal(2UL, stale.PrimaryGeneration);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldBootstrapGrantAndPrimaryTogetherAndReplayTheOutcome()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var reservation = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, operation);
        var scenario = new AggregateScenario<PlatformOperator>(owner);

        // Act
        var result = owner.AcceptBootstrap(operation, 0, grant, lineage, 1, reservation, SystemActor);
        var retry = owner.AcceptBootstrap(operation, 0, grant, lineage, 1, reservation, SystemActor);
        var replay = new PlatformOperator(user, operation);
        new AggregateScenario<PlatformOperator>(replay).Given(scenario.PendingEvents.ToArray());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.IsType<PlatformOperatorBootstrapGrantAndMarkAccepted>(Assert.Single(scenario.PendingEvents));
        Assert.True(replay.IsActive);
        Assert.True(replay.IsPrimary);
        Assert.Equal(grant, replay.GrantEpisodeId);
        Assert.Equal(1UL, replay.PrimaryGeneration);
        Assert.IsType<PlatformOperatorBootstrapGrantAndMarkAccepted>(replay.SelectedOutcome);
    }

    [Fact]
    public void ShouldNeverRegrantPreviouslyRevokedUserFromBootstrap()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var history = GrantHistory(user, grant);
        var revoke = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, revoke);
        var revoked = new AggregateScenario<PlatformOperator>(owner).Given(history);
        Assert.True(owner.Revoke(revoke, 1, grant, ActorId, Actor, "rotation").IsSuccess);
        var bootstrap = Uuid.CreateVersion4();
        var target = new PlatformOperator(user, bootstrap);
        var scenario = new AggregateScenario<PlatformOperator>(target).Given(history.Concat(revoked.PendingEvents).ToArray());

        // Act
        var result = target.AcceptBootstrap(bootstrap, 2, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
            Uuid.CreateVersion4(), SystemActor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(target.IsActive);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldBootstrapNeverGrantedUserAtLaterGenerationAfterAnExactFence()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var firstOperation = Uuid.CreateVersion4();
        var firstGrant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var firstReservation = Uuid.CreateVersion4();
        var fenced = new PlatformOperator(user, firstOperation);
        var fenceScenario = new AggregateScenario<PlatformOperator>(fenced);
        Assert.True(fenced.FencePrimaryMark(firstOperation, 0, firstGrant, lineage, 1, firstReservation).IsSuccess);
        var history = fenceScenario.PendingEvents.ToArray();
        var secondOperation = Uuid.CreateVersion4();
        var secondGrant = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, secondOperation);
        var scenario = new AggregateScenario<PlatformOperator>(owner).Given(history);

        // Act
        var result = owner.AcceptBootstrap(secondOperation, 1, secondGrant, lineage, 2, Uuid.CreateVersion4(), SystemActor);
        var delayed = new PlatformOperator(user, firstOperation);
        new AggregateScenario<PlatformOperator>(delayed).Given(history.Concat(scenario.PendingEvents).ToArray());
        var old = delayed.AcceptBootstrap(firstOperation, 0, firstGrant, lineage, 1, firstReservation, SystemActor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(owner.IsActive);
        Assert.True(owner.IsPrimary);
        Assert.Equal(2UL, owner.PrimaryGeneration);
        Assert.False(old.IsSuccess);
        Assert.IsType<PlatformOperatorBootstrapGrantAndMarkAccepted>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldRefuseLosingMarkGivenCompetingRevocationWonTheTargetPosition()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var history = GrantHistory(user, grant);
        var revokeId = Uuid.CreateVersion4();
        var revokeOwner = new PlatformOperator(user, revokeId);
        var revoked = new AggregateScenario<PlatformOperator>(revokeOwner).Given(history);
        Assert.True(revokeOwner.Revoke(revokeId, 1, grant, ActorId, Actor, "rotation").IsSuccess);
        var markId = Uuid.CreateVersion4();
        var actual = new PlatformOperator(user, markId);
        var scenario = new AggregateScenario<PlatformOperator>(actual).Given(history.Concat(revoked.PendingEvents).ToArray());

        // Act
        var mark = actual.AcceptPrimaryMark(markId, 1, grant, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4());

        // Assert
        Assert.False(mark.IsSuccess);
        Assert.False(actual.IsActive);
        Assert.Empty(scenario.PendingEvents);
    }

    static DomainEvent[] GrantHistory(Uuid user, Uuid grant)
    {
        var owner = new PlatformOperator(user, grant);
        var scenario = new AggregateScenario<PlatformOperator>(owner);
        Assert.True(owner.Grant(grant, 0, ActorId, Actor, "initial grant").IsSuccess);
        return scenario.PendingEvents.ToArray();
    }

    static DomainEvent[] MarkedHistory(Uuid user, Uuid grant, Uuid lineage, ulong generation, Uuid reservation)
    {
        var history = GrantHistory(user, grant);
        var mark = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, mark);
        var scenario = new AggregateScenario<PlatformOperator>(owner).Given(history);
        Assert.True(owner.AcceptPrimaryMark(mark, 1, grant, lineage, generation, reservation).IsSuccess);
        return history.Concat(scenario.PendingEvents).ToArray();
    }
}
