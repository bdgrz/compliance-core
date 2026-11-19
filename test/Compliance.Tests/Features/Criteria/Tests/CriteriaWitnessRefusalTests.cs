using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaWitnessRefusalTests
{
    static readonly ActorAttribution Actor = new("operator", "issuer");
    static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldFenceDelayedPinGivenExactMissingWitnessRefusal()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var attempt = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", selectedPublicationAttemptId: attempt);
        var start = Started(edition, draft, attempt, 1, Uuid.CreateVersion4());
        var scenario = new AggregateScenario<CriteriaEntry>(owner);

        // Act
        var refusal = owner.RefuseWitness(start);
        var exact = owner.RefuseWitness(start);
        var changed = owner.RefuseWitness(start with { WitnessIncarnationId = Uuid.CreateVersion4() });
        var delayedPin = owner.PinWitness(start);

        // Assert
        Assert.True(refusal.IsSuccess);
        Assert.True(exact.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.False(delayedPin.IsSuccess);
        var fact = Assert.IsType<CriterionWitnessRefused>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(start.Metadata.EventId, fact.PublicationStartedEventId);
    }

    [Fact]
    public void ShouldRemainPendingGivenUnresolvedEntryMutation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var attempt = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation, attempt);
        var intent = Create(edition, draft, operation);
        var admission = Admission(owner, intent);
        Assert.True(owner.Propose(intent, 1, Actor, admission, null, Claim(intent, admission)).IsSuccess);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var before = scenario.PendingEvents.ToArray();
        var start = Started(edition, draft, attempt, 1, Uuid.CreateVersion4());

        // Act
        var result = owner.RefuseWitness(start);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
        Assert.Null(owner.SelectedWitnessRefusal);
    }

    [Fact]
    public void ShouldNotRefuseGivenExactAcceptedCriterionCanBePinned()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var attempt = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation, attempt);
        var intent = Create(edition, draft, operation);
        var admission = Admission(owner, intent);
        Assert.True(owner.Propose(intent, 1, Actor, admission, null, Claim(intent, admission)).IsSuccess);
        var proposed = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(owner).PendingEvents));
        // This synthetic draft-head evidence exercises the aggregate only; it does not qualify a source read.
        var evidence = new CriteriaEntryDecisionEvidence("draft", Uuid.CreateVersion4(), 0, 1, 2);
        Assert.True(owner.Decide(operation, 1, proposed.Metadata.EventId, true, null, evidence).IsSuccess);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var before = scenario.PendingEvents.ToArray();
        var start = Started(edition, draft, attempt, 1, owner.CurrentIncarnationId);

        // Act
        var result = owner.RefuseWitness(start);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldRejectStaleRefusalGivenCompetingEntryProposalCommitsFirst()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var attempt = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var refusalOwner = new CriteriaEntry(edition, "CC6.1", selectedPublicationAttemptId: attempt);
        var proposalOwner = new CriteriaEntry(edition, "CC6.1", operation);
        var start = Started(edition, draft, attempt, 1, Uuid.CreateVersion4());
        var intent = Create(edition, draft, operation);
        Assert.True(refusalOwner.RefuseWitness(start).IsSuccess);
        var admission = Admission(proposalOwner, intent);
        Assert.True(proposalOwner.Propose(intent, 1, Actor, admission, null, Claim(intent, admission)).IsSuccess);
        var refusal = new AggregateScenario<CriteriaEntry>(refusalOwner).PendingEvents.ToArray();
        var proposal = new AggregateScenario<CriteriaEntry>(proposalOwner).PendingEvents.ToArray();
        var store = new InMemoryEventStore();
        await store.AppendAsync(proposalOwner.Stream, 0, proposal);

        // Act
        var failure = await Record.ExceptionAsync(async () => await store.AppendAsync(refusalOwner.Stream, 0, refusal));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
    }

    static CriterionChangeIntent Create(Uuid edition, Uuid draft, Uuid operation) =>
        new(edition, draft, "CC6.1", operation, 0, "create", "criterion", "security", null, null, "summary");
    static AccessAdmissionEvidence Admission(CriteriaEntry owner, CriterionChangeIntent intent)
    {
        var identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/actor", "generation", 1);
        var policy = new AccessAdmissionSourceVersion("stream://bdgrz/policies/current", "generation", 1);
        return new AccessAdmissionEvidence(Uuid.CreateVersion4(), Actor.Subject, Actor.Issuer, intent.OperationId,
            owner.Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1), At, "policy-v1", identity, policy)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = new AccessAdmissionSourceVersion("stream://bdgrz/operators/actor", "generation", 1)
        };
    }
    static CriterionEntryOperationClaimed Claim(CriterionChangeIntent intent, AccessAdmissionEvidence admission)
    {
        var claim = new CriterionEntryOperationClaimed(intent, null, 1, Actor, admission);
        claim.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            CriteriaEntryOperation.IdentityOf(intent.EditionId, intent.Identifier, intent.OperationId), 1, At));
        return claim;
    }

    static CriteriaPublicationStarted Started(Uuid edition, Uuid draft, Uuid attempt, long ordinal, Uuid incarnation)
    {
        var start = new CriteriaPublicationStarted(edition, draft, attempt, 1, ordinal, "CC6.1", incarnation,
            "edition_1", 1, Uuid.CreateVersion4(), Actor, null);
        start.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition, 1, At));
        return start;
    }
}
