using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class EditionLabelRefusalTests
{
    static readonly ActorAttribution Actor = new("operator", "bdgrz");

    [Fact]
    public void ShouldRefuseWithHolderEvidenceGivenDifferentLiveClaim()
    {
        // Arrange
        var holderEdition = Uuid.CreateVersion4();
        var holderOperation = Uuid.CreateVersion4();
        var holder = Claim(holderEdition, holderOperation);
        Assert.True(holder.Reserve(holderEdition, holderOperation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        var held = Facts(holder);
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var contender = Claim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(contender).Given(held);

        // Act
        var result = contender.RefuseContested(edition, operation, 2, requestId, Actor);

        // Assert
        Assert.True(result.IsSuccess);
        var fact = Assert.IsType<EditionLabelReservationRefused>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(((EditionLabelReserved)held[0]).Metadata.EventId, fact.HolderReservationEventId);
        Assert.Equal(holderEdition, fact.HolderEditionId);
        Assert.Equal(holderOperation, fact.HolderOperationId);
        Assert.Equal(1, fact.HolderGeneration);
        Assert.Equal(2, fact.RequestedGeneration);
        Assert.True(contender.IsHeldBy(holderEdition, holderOperation, 1));
    }

    [Fact]
    public void ShouldReturnNoFactGivenMissingHolderOrChangedGeneration()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = Claim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);

        // Act
        var result = claim.RefuseContested(edition, operation, 1, Uuid.CreateVersion4(), Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldReplayOriginalRefusalGivenHolderLaterReleasesAndGenerationChanges()
    {
        // Arrange
        var holderEdition = Uuid.CreateVersion4();
        var holderOperation = Uuid.CreateVersion4();
        var holder = Claim(holderEdition, holderOperation);
        Assert.True(holder.Reserve(holderEdition, holderOperation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        var held = Facts(holder);
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = Uuid.CreateVersion4();
        var contender = Claim(edition, operation);
        new AggregateScenario<EditionLabelClaim>(contender).Given(held);
        Assert.True(contender.RefuseContested(edition, operation, 2, request, Actor).IsSuccess);
        var refusal = Facts(contender);
        var released = Claim(holderEdition, holderOperation);
        new AggregateScenario<EditionLabelClaim>(released).Given(held.Concat(refusal).ToArray());
        Assert.True(released.Release(holderEdition, holderOperation, 1, Uuid.CreateVersion4(), "cancelled").IsSuccess);
        var release = Facts(released);
        var successorEdition = Uuid.CreateVersion4();
        var successorOperation = Uuid.CreateVersion4();
        var successor = Claim(successorEdition, successorOperation);
        new AggregateScenario<EditionLabelClaim>(successor).Given(held.Concat(refusal).Concat(release).ToArray());
        Assert.True(successor.Reserve(successorEdition, successorOperation, 2, Uuid.CreateVersion4(), Actor).IsSuccess);
        var history = held.Concat(refusal).Concat(release).Concat(Facts(successor)).ToArray();
        var replay = Claim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(replay).Given(history);

        // Act
        var exact = replay.RefuseContested(edition, operation, 2, request, Actor);
        var rebased = replay.RefuseContested(edition, operation, 3, request, Actor);
        var changed = replay.RefuseContested(edition, operation, 2, Uuid.CreateVersion4(), Actor);
        var reservation = replay.Reserve(edition, operation, 3, request, Actor);

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.False(rebased.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.False(reservation.IsSuccess);
        Assert.Equal(2, replay.RefusalFor(edition, operation)!.RequestedGeneration);
        Assert.True(replay.IsHeldBy(successorEdition, successorOperation, 2));
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldRejectStaleRefusalGivenCompetingReleaseWonTheAppend()
    {
        // Arrange
        var holderEdition = Uuid.CreateVersion4();
        var holderOperation = Uuid.CreateVersion4();
        var holder = Claim(holderEdition, holderOperation);
        Assert.True(holder.Reserve(holderEdition, holderOperation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        var held = Facts(holder);
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var contender = Claim(edition, operation);
        new AggregateScenario<EditionLabelClaim>(contender).Given(held);
        Assert.True(contender.RefuseContested(edition, operation, 2, Uuid.CreateVersion4(), Actor).IsSuccess);
        var staleRefusal = Facts(contender);
        var releasing = Claim(holderEdition, holderOperation);
        new AggregateScenario<EditionLabelClaim>(releasing).Given(held);
        Assert.True(releasing.Release(holderEdition, holderOperation, 1, Uuid.CreateVersion4(), "cancelled").IsSuccess);
        var store = new InMemoryEventStore();
        await store.AppendAsync(holder.Stream, 0, held);
        await store.AppendAsync(holder.Stream, 1, Facts(releasing));

        // Act
        var failure = await Record.ExceptionAsync(async () => await store.AppendAsync(holder.Stream, 1, staleRefusal));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
    }

    static EditionLabelClaim Claim(Uuid edition, Uuid operation) => new("edition_1", edition, operation);
    static DomainEvent[] Facts(EditionLabelClaim claim) => new AggregateScenario<EditionLabelClaim>(claim).PendingEvents.ToArray();
}
