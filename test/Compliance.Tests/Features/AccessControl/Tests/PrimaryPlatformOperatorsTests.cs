using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PrimaryPlatformOperatorsTests
{
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly ActorAttribution Actor = new(ActorId.ToString(), "bdgrz");
    static readonly ActorAttribution SystemActor = new("portia:system", "Portia");

    [Fact]
    public void ShouldConfirmInitialSeatGivenTheExactTargetBootstrapOutcomeIsObserved()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var plan = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(operation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(owner);
        Assert.True(owner.ReserveInitial(operation, 0, user, grant, lineage, plan, SystemActor).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var target = Attach(new PlatformOperatorBootstrapGrantAndMarkAccepted(user, operation, 0, grant, lineage,
            reservation.Generation, reservation.Metadata.EventId, SystemActor), user, 1);

        // Act
        var result = owner.RecordTargetOutcome(target);
        var retry = owner.RecordTargetOutcome(target);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.True(owner.HasConfirmedInitialPrimary);
        Assert.Equal(1, owner.ConfirmedSeats);
        Assert.Equal(1, owner.OccupiedSeats);
        Assert.IsType<PrimaryPlatformOperatorAppointed>(owner.SelectedOutcome);
        Assert.Equal(2, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldCompleteBootstrapGivenTheInitialPrimaryIsConfirmed()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var plan = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(operation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(owner);
        Assert.True(owner.ReserveInitial(operation, 0, user, grant, plan, plan, SystemActor).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var target = Attach(new PlatformOperatorBootstrapGrantAndMarkAccepted(user, operation, 0, grant, plan,
            reservation.Generation, reservation.Metadata.EventId, SystemActor), user, 1);

        // Act
        var premature = owner.CompleteBootstrap(plan);
        Assert.True(owner.RecordTargetOutcome(target).IsSuccess);
        var complete = owner.CompleteBootstrap(plan);
        var retry = owner.CompleteBootstrap(plan);
        var changed = owner.CompleteBootstrap(Uuid.CreateVersion4());

        // Assert
        Assert.False(premature.IsSuccess);
        Assert.True(complete.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.True(owner.IsBootstrapComplete);
        Assert.IsType<PlatformOperatorBootstrapCompleted>(scenario.PendingEvents[^1]);
    }

    [Fact]
    public void ShouldRejectTargetFactsGivenTheyAreUnattachedOrHaveTheWrongOwner()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(operation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(owner);
        Assert.True(owner.ReserveInitial(operation, 0, user, grant, lineage, Uuid.CreateVersion4(), SystemActor).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var unattached = new PlatformOperatorBootstrapGrantAndMarkAccepted(user, operation, 0, grant, lineage, 1,
            reservation.Metadata.EventId, SystemActor);
        var wrongOwner = Attach(new PlatformOperatorBootstrapGrantAndMarkAccepted(user, operation, 0, grant, lineage, 1,
            reservation.Metadata.EventId, SystemActor), Uuid.CreateVersion4(), 1);

        // Act
        var missing = owner.RecordTargetOutcome(unattached);
        var wrong = owner.RecordTargetOutcome(wrongOwner);

        // Assert
        Assert.False(missing.IsSuccess);
        Assert.False(wrong.IsSuccess);
        Assert.Equal(0, owner.ConfirmedSeats);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseAThirdSeatGivenOneConfirmedAndOnePending()
    {
        // Arrange
        var initial = InitialConfirmed();
        var second = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(second);
        var pending = new AggregateScenario<PrimaryPlatformOperators>(owner).Given(initial.History);
        Assert.True(owner.ReservePrimary(second, 2, Uuid.CreateVersion4(), Uuid.CreateVersion4(), ActorId, Actor).IsSuccess);
        var third = Uuid.CreateVersion4();
        var actual = new PrimaryPlatformOperators(third);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(actual).Given(initial.History.Concat(pending.PendingEvents).ToArray());

        // Act
        var result = actual.ReservePrimary(third, 3, Uuid.CreateVersion4(), Uuid.CreateVersion4(), ActorId, Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, actual.OccupiedSeats);
        Assert.Equal(1, actual.ConfirmedSeats);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldKeepLastConfirmedPrimaryGivenCompetingReleases()
    {
        // Arrange
        var first = InitialConfirmed();
        var second = AdditionalConfirmed(first.History);
        var firstRelease = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(firstRelease);
        var released = new AggregateScenario<PrimaryPlatformOperators>(owner).Given(second.History);
        Assert.True(owner.Release(firstRelease, 4, first.Reservation.UserId, first.Reservation.Generation,
            ActorId, Actor, "rotation").IsSuccess);
        var secondRelease = Uuid.CreateVersion4();
        var actual = new PrimaryPlatformOperators(secondRelease);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(actual).Given(second.History.Concat(released.PendingEvents).ToArray());

        // Act
        var stale = actual.Release(secondRelease, 4, second.Reservation.UserId, second.Reservation.Generation, ActorId, Actor, "rotation");
        var refreshed = actual.Release(secondRelease, 5, second.Reservation.UserId, second.Reservation.Generation, ActorId, Actor, "rotation");

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.False(refreshed.IsSuccess);
        Assert.Equal(1, actual.ConfirmedSeats);
        Assert.Equal(2, actual.OccupiedSeats);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldKeepReleasedSeatGivenTheTargetIsNotYetUnmarkedThenPermitANewReservation()
    {
        // Arrange
        var first = InitialConfirmed();
        var second = AdditionalConfirmed(first.History);
        var releaseId = Uuid.CreateVersion4();
        var releasing = new PrimaryPlatformOperators(releaseId);
        var released = new AggregateScenario<PrimaryPlatformOperators>(releasing).Given(second.History);
        Assert.True(releasing.Release(releaseId, 4, first.Reservation.UserId, first.Reservation.Generation,
            ActorId, Actor, "rotation").IsSuccess);
        var release = Assert.IsType<PrimaryPlatformOperatorSeatReleased>(Assert.Single(released.PendingEvents));
        var wrong = Attach(new PlatformOperatorPrimaryUnmarked(first.Reservation.UserId, releaseId, 2,
            first.Reservation.GrantEpisodeId, first.Reservation.LineageId, first.Reservation.Generation,
            first.Reservation.Metadata.EventId, Uuid.CreateVersion4()), first.Reservation.UserId, 3);
        var correct = Attach(wrong with { ReleaseEventId = release.Metadata.EventId }, first.Reservation.UserId, 3);

        // Act
        var rejected = releasing.RecordTargetUnmark(wrong);
        var accepted = releasing.RecordTargetUnmark(correct);
        var retry = releasing.RecordTargetUnmark(correct);
        var afterCleanup = second.History.Concat(released.PendingEvents).ToArray();
        var newOperation = Uuid.CreateVersion4();
        var next = new PrimaryPlatformOperators(newOperation);
        new AggregateScenario<PrimaryPlatformOperators>(next).Given(afterCleanup);
        var reservation = next.ReservePrimary(newOperation, 6, Uuid.CreateVersion4(), Uuid.CreateVersion4(), ActorId, Actor);

        // Assert
        Assert.False(rejected.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(1, releasing.OccupiedSeats);
        Assert.True(reservation.IsSuccess);
        Assert.Equal(2, next.OccupiedSeats);
        Assert.Equal(1, next.ConfirmedSeats);
    }

    [Fact]
    public void ShouldClosePendingSeatGivenItsExactTargetFenceIsNotYetObserved()
    {
        // Arrange
        var initial = InitialConfirmed();
        var operation = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(operation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(owner).Given(initial.History);
        Assert.True(owner.ReservePrimary(operation, 2, user, grant, ActorId, Actor).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var fence = Attach(new PlatformOperatorPrimaryMarkFenced(user, operation, 1, grant, reservation.LineageId,
            reservation.Generation, reservation.Metadata.EventId), user, 2);
        var late = Attach(new PlatformOperatorPrimaryMarkAccepted(user, operation, 1, grant, reservation.LineageId,
            reservation.Generation, reservation.Metadata.EventId), user, 2);

        // Act
        var refused = owner.RecordTargetOutcome(fence);
        var retry = owner.RecordTargetOutcome(fence);
        var delayed = owner.RecordTargetOutcome(late);

        // Assert
        Assert.True(refused.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(delayed.IsSuccess);
        Assert.Equal(1, owner.OccupiedSeats);
        Assert.Equal(1, owner.ConfirmedSeats);
        Assert.IsType<PrimaryPlatformOperatorSeatRefused>(owner.SelectedOutcome);
    }

    [Fact]
    public void ShouldRetryBootstrapCandidateGivenTheExactFenceIsObservedAtANewGeneration()
    {
        // Arrange
        var firstOperation = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var plan = Uuid.CreateVersion4();
        var first = new PrimaryPlatformOperators(firstOperation);
        var firstScenario = new AggregateScenario<PrimaryPlatformOperators>(first);
        Assert.True(first.ReserveInitial(firstOperation, 0, user, Uuid.CreateVersion4(), lineage, plan, SystemActor).IsSuccess);
        var initial = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(firstScenario.PendingEvents));
        var fence = Attach(new PlatformOperatorPrimaryMarkFenced(user, firstOperation, 0, initial.GrantEpisodeId,
            lineage, initial.Generation, initial.Metadata.EventId), user, 1);
        Assert.True(first.RecordTargetOutcome(fence).IsSuccess);
        var retryOperation = Uuid.CreateVersion4();
        var retry = new PrimaryPlatformOperators(retryOperation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(retry).Given(firstScenario.PendingEvents.ToArray());
        var grant = Uuid.CreateVersion4();

        // Act
        var changedPlan = retry.ReserveInitial(retryOperation, 2, user, grant, lineage, Uuid.CreateVersion4(), SystemActor);
        var changedLineage = retry.ReserveInitial(retryOperation, 2, user, grant, Uuid.CreateVersion4(), plan, SystemActor);
        var reserved = retry.ReserveInitial(retryOperation, 2, user, grant, lineage, plan, SystemActor);
        var next = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var target = Attach(new PlatformOperatorBootstrapGrantAndMarkAccepted(user, retryOperation, 1, grant, lineage,
            next.Generation, next.Metadata.EventId, SystemActor), user, 2);
        var confirmed = retry.RecordTargetOutcome(target);

        // Assert
        Assert.False(changedPlan.IsSuccess);
        Assert.False(changedLineage.IsSuccess);
        Assert.True(reserved.IsSuccess);
        Assert.Equal(2UL, next.Generation);
        Assert.True(confirmed.IsSuccess);
        Assert.Equal(1, retry.ConfirmedSeats);
    }

    [Fact]
    public void ShouldRejectTargetFactGivenItsEnvelopeDoesNotFollowTheExpectedRevision()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(operation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(owner);
        Assert.True(owner.ReserveInitial(operation, 0, user, grant, lineage, Uuid.CreateVersion4(), SystemActor).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var target = Attach(new PlatformOperatorBootstrapGrantAndMarkAccepted(user, operation, 0, grant, lineage,
            reservation.Generation, reservation.Metadata.EventId, SystemActor), user, 2);

        // Act
        var result = owner.RecordTargetOutcome(target);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Single(scenario.PendingEvents);
    }

    static (DomainEvent[] History, PrimaryPlatformOperatorSeatReserved Reservation) InitialConfirmed()
    {
        var operation = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(operation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(owner);
        Assert.True(owner.ReserveInitial(operation, 0, user, grant, lineage, Uuid.CreateVersion4(), SystemActor).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var target = Attach(new PlatformOperatorBootstrapGrantAndMarkAccepted(user, operation, 0, grant, lineage,
            reservation.Generation, reservation.Metadata.EventId, SystemActor), user, 1);
        Assert.True(owner.RecordTargetOutcome(target).IsSuccess);
        return (scenario.PendingEvents.ToArray(), reservation);
    }

    static (DomainEvent[] History, PrimaryPlatformOperatorSeatReserved Reservation) AdditionalConfirmed(DomainEvent[] history)
    {
        var operation = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var owner = new PrimaryPlatformOperators(operation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(owner).Given(history);
        Assert.True(owner.ReservePrimary(operation, 2, user, grant, ActorId, Actor).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var target = Attach(new PlatformOperatorPrimaryMarkAccepted(user, operation, 1, grant, reservation.LineageId,
            reservation.Generation, reservation.Metadata.EventId), user, 2);
        Assert.True(owner.RecordTargetOutcome(target).IsSuccess);
        return (history.Concat(scenario.PendingEvents).ToArray(), reservation);
    }

    static T Attach<T>(T fact, Uuid ownerUserId, ulong version) where T : DomainEvent
    {
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), PlatformOperator.IdentityOf(ownerUserId), version,
            new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)));
        return fact;
    }
}
