using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaPublicationRaceTests
{
    static readonly ActorAttribution Actor = new("operator", "issuer");
    static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRejectStalePublicationGivenAbortWonBeforeWitnessFence()
    {
        // Arrange
        var (history, editionId, attempt) = StartedEdition();
        var abortOwner = new CriteriaEdition(editionId, attempt);
        var publishOwner = new CriteriaEdition(editionId, attempt);
        var abortScenario = new AggregateScenario<CriteriaEdition>(abortOwner).Given(history);
        var publishScenario = new AggregateScenario<CriteriaEdition>(publishOwner).Given(history);
        Assert.True(abortOwner.AbortPublication(attempt, "witness refused").IsSuccess);
        Assert.True(publishOwner.Publish(attempt, Uuid.CreateVersion4()).IsSuccess);
        var abort = Assert.IsType<CriteriaPublicationAborted>(Assert.Single(abortScenario.PendingEvents));
        var store = new InMemoryEventStore();
        await store.AppendAsync(abortOwner.Stream, 0, history);
        await store.AppendAsync(abortOwner.Stream, (ulong)history.Length, [abort]);
        var witness = new CriteriaEntry(editionId, abort.WitnessIdentifier, selectedPublicationAttemptId: attempt);
        Assert.True(witness.FenceWitness(abort).IsSuccess);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(publishOwner.Stream, (ulong)history.Length, publishScenario.PendingEvents.ToArray()));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
        Assert.NotNull(witness.SelectedWitnessFence);
    }

    [Fact]
    public async Task ShouldRejectStaleAbortGivenPublicationWonAtTheEditionPosition()
    {
        // Arrange
        var (history, editionId, attempt) = StartedEdition();
        var abortOwner = new CriteriaEdition(editionId, attempt);
        var publishOwner = new CriteriaEdition(editionId, attempt);
        var abortScenario = new AggregateScenario<CriteriaEdition>(abortOwner).Given(history);
        var publishScenario = new AggregateScenario<CriteriaEdition>(publishOwner).Given(history);
        Assert.True(abortOwner.AbortPublication(attempt, "witness refused").IsSuccess);
        Assert.True(publishOwner.Publish(attempt, Uuid.CreateVersion4()).IsSuccess);
        var store = new InMemoryEventStore();
        await store.AppendAsync(publishOwner.Stream, 0, history);
        await store.AppendAsync(publishOwner.Stream, (ulong)history.Length, publishScenario.PendingEvents.ToArray());

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(abortOwner.Stream, (ulong)history.Length, abortScenario.PendingEvents.ToArray()));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
    }

    [Fact]
    public async Task ShouldRejectStaleAbandonmentGivenPublicationStartWonTheEditionPosition()
    {
        // Arrange
        var (history, editionId) = DraftEdition();
        var attempt = Uuid.CreateVersion4();
        var abandonId = Uuid.CreateVersion4();
        var starting = new CriteriaEdition(editionId, attempt);
        var abandoning = new CriteriaEdition(editionId, abandonId);
        var startScenario = new AggregateScenario<CriteriaEdition>(starting).Given(history);
        var abandonScenario = new AggregateScenario<CriteriaEdition>(abandoning).Given(history);
        var begin = new BeginCriteriaPublication(editionId, attempt, "CC6.1", Uuid.CreateVersion4());
        var abandon = new AbandonCriteriaEditionDraft(editionId, abandonId);
        Assert.True(starting.BeginPublication(attempt, begin.WitnessIdentifier, begin.WitnessIncarnation,
            Actor, Admission(starting, attempt, begin)).IsSuccess);
        Assert.True(abandoning.Abandon(abandonId, Actor, Admission(abandoning, abandonId, abandon)).IsSuccess);
        var store = new InMemoryEventStore();
        await store.AppendAsync(starting.Stream, 0, history);
        await store.AppendAsync(starting.Stream, (ulong)history.Length, startScenario.PendingEvents.ToArray());

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(abandoning.Stream, (ulong)history.Length, abandonScenario.PendingEvents.ToArray()));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
    }

    [Fact]
    public void ShouldKeepNewLabelHolderGivenRetryOfAnOlderGenerationRelease()
    {
        // Arrange
        var oldEdition = Uuid.CreateVersion4();
        var oldOperation = Uuid.CreateVersion4();
        var old = new EditionLabelClaim("edition_1", oldEdition, oldOperation);
        var terminal = Uuid.CreateVersion4();
        Assert.True(old.Reserve(oldEdition, oldOperation, 1, Uuid.CreateVersion4(), Actor).IsSuccess);
        Assert.True(old.Release(oldEdition, oldOperation, 1, terminal, "cancelled").IsSuccess);
        var oldHistory = new AggregateScenario<EditionLabelClaim>(old).PendingEvents.ToArray();
        var currentEdition = Uuid.CreateVersion4();
        var currentOperation = Uuid.CreateVersion4();
        var next = new EditionLabelClaim("edition_1", currentEdition, currentOperation);
        new AggregateScenario<EditionLabelClaim>(next).Given(oldHistory);
        Assert.True(next.Reserve(currentEdition, currentOperation, 2, Uuid.CreateVersion4(), Actor).IsSuccess);
        var replay = new EditionLabelClaim("edition_1", oldEdition, oldOperation);
        var scenario = new AggregateScenario<EditionLabelClaim>(replay).Given(oldHistory.Concat(
            new AggregateScenario<EditionLabelClaim>(next).PendingEvents).ToArray());

        // Act
        var retry = replay.Release(oldEdition, oldOperation, 1, terminal, "cancelled");

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.True(replay.IsHeldBy(currentEdition, currentOperation, 2));
        Assert.Empty(scenario.PendingEvents);
    }

    static (DomainEvent[] History, Uuid EditionId, Uuid Attempt) StartedEdition()
    {
        var (history, editionId) = DraftEdition();
        var attempt = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(editionId, attempt);
        var scenario = new AggregateScenario<CriteriaEdition>(owner).Given(history);
        var request = new BeginCriteriaPublication(editionId, attempt, "CC6.1", Uuid.CreateVersion4());
        Assert.True(owner.BeginPublication(attempt, request.WitnessIdentifier, request.WitnessIncarnation,
            Actor, Admission(owner, attempt, request)).IsSuccess);
        return (history.Concat(scenario.PendingEvents).ToArray(), editionId, attempt);
    }

    static (DomainEvent[] History, Uuid EditionId) DraftEdition()
    {
        var editionId = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(editionId, operation);
        var request = new DraftCriteriaEdition(editionId, operation, "tsc", "edition_1");
        Assert.True(owner.Draft(operation, "tsc", "edition_1", Actor, Admission(owner, operation, request)).IsSuccess);
        Assert.True(owner.ActivateDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        Assert.True(owner.CompleteDraft(operation, 1, Uuid.CreateVersion4()).IsSuccess);
        return (new AggregateScenario<CriteriaEdition>(owner).PendingEvents.ToArray(), editionId);
    }

    static AccessAdmissionEvidence Admission<T>(CriteriaEdition owner, Uuid operation, T request) where T : notnull
    {
        var identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/actor", "generation", 1);
        var policy = new AccessAdmissionSourceVersion("stream://bdgrz/policies/current", "generation", 1);
        return new AccessAdmissionEvidence(Uuid.CreateVersion4(), Actor.Subject, Actor.Issuer, operation,
            owner.Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(request), 1), At,
            "policy-v1", identity, policy)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = new AccessAdmissionSourceVersion("stream://bdgrz/operators/actor", "generation", 1)
        };
    }
}
