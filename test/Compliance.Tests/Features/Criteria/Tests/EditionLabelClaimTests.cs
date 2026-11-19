using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class EditionLabelClaimTests
{
    static readonly ActorAttribution Actor = new("operator-1", "bdgrz");

    [Theory]
    [InlineData("2017_tsc_2022_pof", "2017_tsc_2022_pof")]
    [InlineData(" 2017_TSC_2022_POF ", "2017_tsc_2022_pof")]
    [InlineData("A_01", "a_01")]
    public void ShouldNormalizeGivenValidStructuredLabel(string input, string expected)
    {
        // Arrange
        var expectedOwner = new EditionLabelClaim(expected);

        // Act
        var valid = EditionLabelClaim.TryNormalize(input, out var normalized);
        var actualOwner = new EditionLabelClaim(input);

        // Assert
        Assert.True(valid);
        Assert.Equal(expected, normalized);
        Assert.Equal(expectedOwner.Id, actualOwner.Id);
        Assert.Equal(expectedOwner.Stream, actualOwner.Stream);
        Assert.Equal(EditionLabelClaim.IdentityOf(expected), EditionLabelClaim.IdentityOf(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("_edition")]
    [InlineData("edition_")]
    [InlineData("edition__one")]
    [InlineData("edition-one")]
    [InlineData("edition\tone")]
    [InlineData("éditión")]
    public void ShouldRefuseGivenInvalidStructuredLabel(string? input)
    {
        // Arrange
        // Act
        var valid = EditionLabelClaim.TryNormalize(input, out _);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void ShouldEnforceDocumentedLabelLengthGivenBoundary()
    {
        // Arrange
        var maximum = new string('a', 128);
        var tooLong = new string('a', 129);

        // Act
        var accepted = EditionLabelClaim.TryNormalize(maximum, out var normalized);
        var refused = EditionLabelClaim.TryNormalize(tooLong, out _);

        // Assert
        Assert.True(accepted);
        Assert.Equal(maximum, normalized);
        Assert.False(refused);
    }

    [Fact]
    public void ShouldReserveOneLabelGivenFreeClaim()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);

        // Act
        var result = claim.Reserve(edition, operation, 1, Uuid.CreateVersion4(), Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EditionLabelReservationStatus.Reserved, result.Value);
        Assert.True(claim.IsHeldBy(edition, operation, 1));
        Assert.False(claim.IsConfirmed);
        Assert.Equal(1, claim.CurrentGeneration);
        Assert.Equal(1, claim.LastGeneration);
        Assert.IsType<EditionLabelReserved>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenEmptyIdentityOrMissingRequestEvidence()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);
        var requestFact = Uuid.CreateVersion4();

        // Act
        var noEdition = claim.Reserve(Uuid.Empty, operation, 1, requestFact, Actor);
        var noOperation = claim.Reserve(edition, Uuid.Empty, 1, requestFact, Actor);
        var noRequest = claim.Reserve(edition, operation, 1, Uuid.Empty, Actor);
        var noGeneration = claim.Reserve(edition, operation, 0, requestFact, Actor);

        // Assert
        Assert.False(noEdition.IsSuccess);
        Assert.False(noOperation.IsSuccess);
        Assert.False(noRequest.IsSuccess);
        Assert.False(noGeneration.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(Uuid.Empty, claim.CurrentEditionId);
    }

    [Fact]
    public void ShouldRefuseReservationGivenAnUnselectedClaim()
    {
        // Arrange
        var claim = NewClaim();
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);

        // Act
        var result = claim.Reserve(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldFenceTerminalRequestWithoutDisturbingAnUnrelatedHolder()
    {
        // Arrange
        var holderEdition = Uuid.CreateVersion4();
        var holderOperation = Uuid.CreateVersion4();
        var holder = NewClaim(holderEdition, holderOperation);
        Assert.True(holder.Reserve(holderEdition, holderOperation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        var terminalEdition = Uuid.CreateVersion4();
        var terminalOperation = Uuid.CreateVersion4();
        var request = Uuid.CreateVersion4();
        var terminal = Uuid.CreateVersion4();
        var fenced = NewClaim(terminalEdition, terminalOperation);
        var scenario = new AggregateScenario<EditionLabelClaim>(fenced).Given(AttachedFacts(holder));

        // Act
        var outcome = fenced.FenceTerminalRequest(terminalEdition, terminalOperation, request, terminal, "cancelled");
        var retry = fenced.FenceTerminalRequest(terminalEdition, terminalOperation, request, terminal, "cancelled");
        var changed = fenced.FenceTerminalRequest(terminalEdition, terminalOperation, request, Uuid.CreateVersion4(), "cancelled");
        var delayed = fenced.Reserve(terminalEdition, terminalOperation, 2, request, Actor);

        // Assert
        Assert.True(outcome.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.False(delayed.IsSuccess);
        Assert.True(fenced.IsRequestFenced(terminalEdition, terminalOperation, request));
        Assert.False(fenced.IsRequestFenced(terminalEdition, terminalOperation, Uuid.CreateVersion4()));
        Assert.True(fenced.IsHeldBy(holderEdition, holderOperation, 1));
        Assert.IsType<EditionLabelReservationFenced>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldFenceAndReleaseGivenTheReservationWonBeforeCancellation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = Uuid.CreateVersion4();
        var terminal = Uuid.CreateVersion4();
        var owner = NewClaim(edition, operation);
        Assert.True(owner.Reserve(edition, operation, 1, request, Actor).IsSuccess);
        var before = AttachedFacts(owner);
        var current = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(current).Given(before);

        // Act
        var outcome = current.FenceTerminalRequest(edition, operation, request, terminal, "cancelled");

        // Assert
        Assert.True(outcome.IsSuccess);
        Assert.Equal(Uuid.Empty, current.CurrentEditionId);
        Assert.Equal(EditionLabelReservationStatus.Released, current.ReservationStatus(edition, operation));
        Assert.Collection(scenario.PendingEvents,
            first => Assert.IsType<EditionLabelReservationFenced>(first),
            second => Assert.IsType<EditionLabelReleased>(second));
        Assert.True(current.FenceTerminalRequest(edition, operation, request, terminal, "cancelled").IsSuccess);
        Assert.Equal(2, scenario.PendingEvents.Count);
    }

    [Fact]
    public async Task ShouldRejectDelayedReservationGivenTheTargetFenceWinsTheAppendRace()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = Uuid.CreateVersion4();
        var terminal = Uuid.CreateVersion4();
        var fence = NewClaim(edition, operation);
        var delayed = NewClaim(edition, operation);
        var store = new InMemoryEventStore();
        Assert.True(fence.FenceTerminalRequest(edition, operation, request, terminal, "cancelled").IsSuccess);
        Assert.True(delayed.Reserve(edition, operation, 1, request, Actor).IsSuccess);

        // Act
        await store.AppendAsync(fence.Stream, 0, AttachedFacts(fence), CancellationToken.None);
        var staleAppend = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(delayed.Stream, 0, AttachedFacts(delayed), CancellationToken.None));
        var replay = new AggregateScenario<EditionLabelClaim>(NewClaim(edition, operation)).Given(AttachedFacts(fence)).Aggregate;
        var rebased = replay.Reserve(edition, operation, 1, request, Actor);

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(staleAppend);
        Assert.False(rebased.IsSuccess);
    }

    [Fact]
    public void ShouldRefuseCompetingEditionWithoutFactGivenHeldLabel()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);
        Assert.True(claim.Reserve(edition, operation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        var competingEdition = Uuid.CreateVersion4();
        var competingOperation = Uuid.CreateVersion4();
        var competingClaim = NewClaim(competingEdition, competingOperation);
        new AggregateScenario<EditionLabelClaim>(competingClaim).Given(AttachedFacts(claim));

        // Act
        var competing = competingClaim.Reserve(competingEdition, competingOperation, 2, Uuid.CreateVersion4(), Actor);

        // Assert
        Assert.False(competing.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, competing.Error!.Kind);
        Assert.Single(scenario.PendingEvents);
        Assert.True(claim.IsHeldBy(edition, operation, 1));
    }

    [Fact]
    public void ShouldReturnSameReservationWithoutFactGivenExactRetry()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);
        var requestFact = Uuid.CreateVersion4();
        Assert.True(claim.Reserve(edition, operation, 1, requestFact, Actor).IsSuccess);

        // Act
        var retry = claim.Reserve(edition, operation, 1, requestFact, Actor);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(EditionLabelReservationStatus.Reserved, retry.Value);
        Assert.Single(scenario.PendingEvents);
        Assert.True(claim.IsHeldBy(edition, operation, 1));
    }

    [Fact]
    public void ShouldConflictWithoutFactGivenReservationIdentityWithChangedIntent()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);
        var requestFact = Uuid.CreateVersion4();
        Assert.True(claim.Reserve(edition, operation, 1, requestFact, Actor).IsSuccess);

        // Act
        var generation = claim.Reserve(edition, operation, 2, requestFact, Actor);
        var evidence = claim.Reserve(edition, operation, 1, Uuid.CreateVersion4(), Actor);
        var actor = claim.Reserve(edition, operation, 1, requestFact, new ActorAttribution("operator-2", "bdgrz"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, generation.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, evidence.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, actor.Error?.Kind);
        Assert.Single(scenario.PendingEvents);
        Assert.True(claim.IsHeldBy(edition, operation, 1));
    }

    [Fact]
    public void ShouldConfirmOnlyExactReservationGivenCommittedEditionEvidence()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);
        var requestFact = Uuid.CreateVersion4();
        var acceptedFact = Uuid.CreateVersion4();
        Assert.True(claim.Reserve(edition, operation, 1, requestFact, Actor).IsSuccess);

        // Act
        var wrongEdition = claim.Confirm(Uuid.CreateVersion4(), operation, 1, acceptedFact);
        var wrongGeneration = claim.Confirm(edition, operation, 2, acceptedFact);
        var noEvidence = claim.Confirm(edition, operation, 1, Uuid.Empty);
        var confirmed = claim.Confirm(edition, operation, 1, acceptedFact);
        var retry = claim.Confirm(edition, operation, 1, acceptedFact);
        var changedEvidence = claim.Confirm(edition, operation, 1, Uuid.CreateVersion4());
        var reserveRetry = claim.Reserve(edition, operation, 1, requestFact, Actor);

        // Assert
        Assert.False(wrongEdition.IsSuccess);
        Assert.False(wrongGeneration.IsSuccess);
        Assert.False(noEvidence.IsSuccess);
        Assert.True(confirmed.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changedEvidence.IsSuccess);
        Assert.True(reserveRetry.IsSuccess);
        Assert.Equal(EditionLabelReservationStatus.Confirmed, reserveRetry.Value);
        Assert.True(claim.IsConfirmed);
        Assert.Collection(scenario.PendingEvents,
            first => Assert.IsType<EditionLabelReserved>(first),
            second => Assert.IsType<EditionLabelConfirmed>(second));
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenStaleReleaseOrMissingRenameEvidence()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);
        var terminal = Uuid.CreateVersion4();
        Assert.True(claim.Reserve(edition, operation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        Assert.True(claim.Confirm(edition, operation, 1, Uuid.CreateVersion4()).IsSuccess);

        // Act
        var wrongEdition = claim.Release(Uuid.CreateVersion4(), operation, 1, terminal, "abandoned");
        var wrongGeneration = claim.Release(edition, operation, 2, terminal, "abandoned");
        var noTerminal = claim.Release(edition, operation, 1, Uuid.Empty, "abandoned");
        var noReplacement = claim.Release(edition, operation, 1, terminal, "renamed");
        var invalidReason = claim.Release(edition, operation, 1, terminal, "other");

        // Assert
        Assert.False(wrongEdition.IsSuccess);
        Assert.False(wrongGeneration.IsSuccess);
        Assert.False(noTerminal.IsSuccess);
        Assert.False(noReplacement.IsSuccess);
        Assert.False(invalidReason.IsSuccess);
        Assert.Equal(2, scenario.PendingEvents.Count);
        Assert.True(claim.IsHeldBy(edition, operation, 1));
        Assert.True(claim.IsConfirmed);
    }

    [Fact]
    public void ShouldReleaseAndPermitNextGenerationGivenExactRenameAndConfirmation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(edition, operation);
        var scenario = new AggregateScenario<EditionLabelClaim>(claim);
        var replacement = Uuid.CreateVersion4();
        Assert.True(claim.Reserve(edition, operation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        Assert.True(claim.Confirm(edition, operation, 1, Uuid.CreateVersion4()).IsSuccess);

        // Act
        var released = claim.Release(edition, operation, 1, Uuid.CreateVersion4(), "renamed", replacement);
        var nextEdition = Uuid.CreateVersion4();
        var nextOperation = Uuid.CreateVersion4();
        var nextClaim = NewClaim(nextEdition, nextOperation);
        new AggregateScenario<EditionLabelClaim>(nextClaim).Given(AttachedFacts(claim));
        var next = nextClaim.Reserve(nextEdition, nextOperation, 2, Uuid.CreateVersion4(), Actor);

        // Assert
        Assert.True(released.IsSuccess);
        Assert.True(next.IsSuccess);
        Assert.Equal(EditionLabelReservationStatus.Reserved, next.Value);
        Assert.True(nextClaim.IsHeldBy(nextEdition, nextOperation, 2));
        Assert.Equal(2, nextClaim.LastGeneration);
        Assert.Collection(scenario.PendingEvents,
            first => Assert.IsType<EditionLabelReserved>(first),
            second => Assert.IsType<EditionLabelConfirmed>(second),
            third => Assert.IsType<EditionLabelReleased>(third));
        Assert.IsType<EditionLabelReserved>(Assert.Single(AttachedFacts(nextClaim)));
    }

    [Fact]
    public void ShouldNotReacquireOrClearNewHolderGivenOldReservationAndReleaseRetries()
    {
        // Arrange
        var firstEdition = Uuid.CreateVersion4();
        var firstOperation = Uuid.CreateVersion4();
        var claim = NewClaim(firstEdition, firstOperation);
        var requestFact = Uuid.CreateVersion4();
        var terminalFact = Uuid.CreateVersion4();
        Assert.True(claim.Reserve(firstEdition, firstOperation, 1, requestFact, Actor).IsSuccess);
        Assert.True(claim.Release(firstEdition, firstOperation, 1, terminalFact, "cancelled").IsSuccess);
        var secondEdition = Uuid.CreateVersion4();
        var secondOperation = Uuid.CreateVersion4();
        var second = NewClaim(secondEdition, secondOperation);
        var firstHistory = AttachedFacts(claim);
        new AggregateScenario<EditionLabelClaim>(second).Given(firstHistory);
        Assert.True(second.Reserve(secondEdition, secondOperation, 2, Uuid.CreateVersion4(), Actor).IsSuccess);
        var replay = NewClaim(firstEdition, firstOperation);
        var scenario = new AggregateScenario<EditionLabelClaim>(replay)
            .Given(firstHistory.Concat(AttachedFacts(second)).ToArray());

        // Act
        var oldReservation = replay.Reserve(firstEdition, firstOperation, 1, requestFact, Actor);
        var oldRelease = replay.Release(firstEdition, firstOperation, 1, terminalFact, "cancelled");
        var staleRelease = replay.Release(firstEdition, firstOperation, 1, Uuid.CreateVersion4(), "cancelled");

        // Assert
        Assert.True(oldReservation.IsSuccess);
        Assert.Equal(EditionLabelReservationStatus.Released, oldReservation.Value);
        Assert.Equal(1, replay.ReservationFor(firstEdition, firstOperation)?.Generation);
        Assert.Equal(requestFact, replay.ReservationFor(firstEdition, firstOperation)?.EditionRequestEventId);
        Assert.True(oldRelease.IsSuccess);
        Assert.False(staleRelease.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.True(replay.IsHeldBy(secondEdition, secondOperation, 2));
    }

    [Fact]
    public void ShouldPermitSameRawOperationIdGivenDifferentEditionAfterRelease()
    {
        // Arrange
        var firstEdition = Uuid.CreateVersion4();
        var secondEdition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var claim = NewClaim(firstEdition, operation);
        Assert.True(claim.Reserve(firstEdition, operation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        Assert.True(claim.Release(firstEdition, operation, 1, Uuid.CreateVersion4(), "refused").IsSuccess);
        var second = NewClaim(secondEdition, operation);
        new AggregateScenario<EditionLabelClaim>(second).Given(AttachedFacts(claim));

        // Act
        var result = second.Reserve(secondEdition, operation, 2, Uuid.CreateVersion4(), Actor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(second.IsHeldBy(secondEdition, operation, 2));
    }

    [Fact]
    public void ShouldRequireNextGenerationGivenPreviouslyReleasedClaim()
    {
        // Arrange
        var oldEdition = Uuid.CreateVersion4();
        var oldOperation = Uuid.CreateVersion4();
        var claim = NewClaim(oldEdition, oldOperation);
        Assert.True(claim.Reserve(oldEdition, oldOperation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        Assert.True(claim.Release(oldEdition, oldOperation, 1, Uuid.CreateVersion4(), "cancelled").IsSuccess);
        var newEdition = Uuid.CreateVersion4();
        var newOperation = Uuid.CreateVersion4();
        var nextClaim = NewClaim(newEdition, newOperation);
        var scenario = new AggregateScenario<EditionLabelClaim>(nextClaim).Given(AttachedFacts(claim));
        var requestFact = Uuid.CreateVersion4();

        // Act
        var reused = nextClaim.Reserve(newEdition, newOperation, 1, requestFact, Actor);
        var skipped = nextClaim.Reserve(newEdition, newOperation, 3, requestFact, Actor);
        var next = nextClaim.Reserve(newEdition, newOperation, 2, requestFact, Actor);

        // Assert
        Assert.False(reused.IsSuccess);
        Assert.False(skipped.IsSuccess);
        Assert.True(next.IsSuccess);
        Assert.Single(scenario.PendingEvents);
        Assert.True(nextClaim.IsHeldBy(newEdition, newOperation, 2));
    }

    [Fact]
    public void ShouldRestoreClaimAndRetryHistoryGivenCommittedFacts()
    {
        // Arrange
        var firstEdition = Uuid.CreateVersion4();
        var firstOperation = Uuid.CreateVersion4();
        var source = NewClaim(firstEdition, firstOperation);
        var requestFact = Uuid.CreateVersion4();
        var terminalFact = Uuid.CreateVersion4();
        Assert.True(source.Reserve(firstEdition, firstOperation, 1, requestFact, Actor).IsSuccess);
        Assert.True(source.Release(firstEdition, firstOperation, 1, terminalFact, "cancelled").IsSuccess);
        var nextEdition = Uuid.CreateVersion4();
        var nextOperation = Uuid.CreateVersion4();
        var second = NewClaim(nextEdition, nextOperation);
        var firstHistory = AttachedFacts(source);
        new AggregateScenario<EditionLabelClaim>(second).Given(firstHistory);
        Assert.True(second.Reserve(nextEdition, nextOperation, 2, Uuid.CreateVersion4(), Actor).IsSuccess);
        var history = firstHistory.Concat(AttachedFacts(second)).ToArray();

        // Act
        var replayed = new AggregateScenario<EditionLabelClaim>(NewClaim(firstEdition, firstOperation)).Given(history).Aggregate;
        var scenario = new AggregateScenario<EditionLabelClaim>(replayed);
        var retry = replayed.Reserve(firstEdition, firstOperation, 1, requestFact, Actor);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(EditionLabelReservationStatus.Released, retry.Value);
        Assert.Equal(2, replayed.LastGeneration);
        Assert.True(replayed.IsHeldBy(nextEdition, nextOperation, 2));
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldCommitOnlyOneReservationGivenCompetingEmptyStreamAppends()
    {
        // Arrange
        var firstEdition = Uuid.CreateVersion4();
        var firstOperation = Uuid.CreateVersion4();
        var secondEdition = Uuid.CreateVersion4();
        var secondOperation = Uuid.CreateVersion4();
        var first = NewClaim(firstEdition, firstOperation);
        var second = NewClaim(secondEdition, secondOperation);
        var store = new InMemoryEventStore();
        Assert.True(first.Reserve(firstEdition, firstOperation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        Assert.True(second.Reserve(secondEdition, secondOperation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        var firstFacts = AttachedFacts(first);
        var secondFacts = AttachedFacts(second);

        // Act
        await store.AppendAsync(first.Stream, 0, firstFacts, CancellationToken.None);
        var failure = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(second.Stream, 0, secondFacts, CancellationToken.None));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
        var persisted = new List<DomainEventRecord>();
        await foreach (var record in store.ReadAsync(first.Stream, 0, CancellationToken.None))
            persisted.Add(record);
        Assert.Single(persisted);
        Assert.IsType<EditionLabelReserved>(persisted[0].Event);
    }

    static EditionLabelClaim NewClaim(Uuid edition = default, Uuid operation = default) =>
        new("2017_tsc_2022_pof", edition, operation);

    static DomainEvent[] AttachedFacts(EditionLabelClaim owner) =>
        new AggregateScenario<EditionLabelClaim>(owner).PendingEvents.ToArray();
}
