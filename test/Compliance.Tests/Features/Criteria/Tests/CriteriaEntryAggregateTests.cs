using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEntryAggregateTests
{
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly ActorAttribution Actor = new("operator", "issuer");
    static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordNoChangeGivenTheExactOutcomeDoesNotAdvanceRevision()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var creation = Uuid.CreateVersion4();
        var initial = new CriteriaEntry(edition, "CC6.1", creation);
        var original = Create(edition, Uuid.CreateVersion4(), creation);
        Assert.True(Propose(initial, original, Actor, Admission(initial, original)).IsSuccess);
        var created = new AggregateScenario<CriteriaEntry>(initial);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(created.PendingEvents));
        Assert.True(initial.Decide(creation, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var history = created.PendingEvents.ToArray();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var scenario = new AggregateScenario<CriteriaEntry>(owner).Given(history);
        var intent = new CriterionSummaryIntent(edition, "CC6.1", operation, 1, original.Summary!);
        var admission = SummaryAdmission(owner, intent);
        var claim = SummaryClaim(intent, admission);

        // Act
        var result = owner.CorrectSummary(intent, 1, Actor, admission, claim);
        var exact = owner.CorrectSummary(intent, 1, Actor, admission, claim);
        var changedIntent = intent with { Summary = "Different." };
        var changed = owner.CorrectSummary(changedIntent, 1, Actor, SummaryAdmission(owner, changedIntent), claim);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(exact.IsSuccess);
        Assert.False(changed.IsSuccess);
        var unchanged = Assert.IsType<CriterionSummaryUnchanged>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(claim.Metadata.EventId, unchanged.OperationClaimEventId);
        Assert.Equal(1, owner.Revision);
        Assert.Equal("accepted", owner.Receipt(operation)?.Status);
        var replay = new CriteriaEntry(edition, "CC6.1", operation);
        new AggregateScenario<CriteriaEntry>(replay).Given(history.Append(unchanged).ToArray());
        Assert.Equal(1, replay.Revision);
        Assert.Equal("accepted", replay.Receipt(operation)?.Status);
    }

    [Fact]
    public void ShouldRefuseNoChangeGivenMissingExactClaim()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var creation = Uuid.CreateVersion4();
        var initial = new CriteriaEntry(edition, "CC6.1", creation);
        var original = Create(edition, Uuid.CreateVersion4(), creation);
        Assert.True(Propose(initial, original, Actor, Admission(initial, original)).IsSuccess);
        var created = new AggregateScenario<CriteriaEntry>(initial);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(created.PendingEvents));
        Assert.True(initial.Decide(creation, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var scenario = new AggregateScenario<CriteriaEntry>(owner).Given(created.PendingEvents.ToArray());
        var intent = new CriterionSummaryIntent(edition, "CC6.1", operation, 1, original.Summary!);

        // Act
        var result = owner.CorrectSummary(intent, 1, Actor, SummaryAdmission(owner, intent), null!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseStaleSummaryGivenItWasClaimedAndTheRevisionAdvanced()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var creation = Uuid.CreateVersion4();
        var initial = new CriteriaEntry(edition, "CC6.1", creation);
        var original = Create(edition, Uuid.CreateVersion4(), creation);
        Assert.True(Propose(initial, original, Actor, Admission(initial, original)).IsSuccess);
        var created = new AggregateScenario<CriteriaEntry>(initial);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(created.PendingEvents));
        Assert.True(initial.Decide(creation, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var history = created.PendingEvents.ToArray();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var scenario = new AggregateScenario<CriteriaEntry>(owner).Given(history);
        var intent = new CriterionSummaryIntent(edition, "CC6.1", operation, 0, "Original product summary.");
        var admission = SummaryAdmission(owner, intent);
        var claim = SummaryClaim(intent, admission);

        // Act
        var result = owner.RefuseSummaryRevision(intent, 1, Actor, admission, claim);
        var exact = owner.RefuseSummaryRevision(intent, 1, Actor, admission, claim);
        var changedIntent = intent with { Summary = "Different." };
        var changed = owner.RefuseSummaryRevision(changedIntent, 1, Actor, SummaryAdmission(owner, changedIntent), claim);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(exact.IsSuccess);
        Assert.False(changed.IsSuccess);
        var refused = Assert.IsType<CriterionSummaryRefused>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(1, refused.ObservedRevision);
        Assert.Equal(claim.Metadata.EventId, refused.OperationClaimEventId);
        Assert.Equal(1, owner.Revision);
        Assert.Equal("refused", owner.Receipt(operation)?.Status);
        var replay = new CriteriaEntry(edition, "CC6.1", operation);
        new AggregateScenario<CriteriaEntry>(replay).Given(history.Append(refused).ToArray());
        Assert.Equal(1, replay.Revision);
        Assert.Equal("refused", replay.Receipt(operation)?.Status);
    }

    [Fact]
    public void ShouldNotRefuseSummaryGivenRevisionStillMatches()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var intent = new CriterionSummaryIntent(edition, "CC6.1", operation, 0, "summary");
        var admission = SummaryAdmission(owner, intent);

        // Act
        var result = owner.RefuseSummaryRevision(intent, 1, Actor, admission, SummaryClaim(intent, admission));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldDeriveOneOwnerForBothEntryKindsGivenTheSameIdentifier()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var criterion = new CriteriaEntry(edition, "CC6.1");

        // Act
        var same = new CriteriaEntry(edition, "CC6.1");
        var other = new CriteriaEntry(edition, "CC6.2");

        // Assert
        Assert.Equal(criterion.Id, same.Id);
        Assert.NotEqual(criterion.Id, other.Id);
        Assert.Equal(CriteriaEntry.StreamFor(edition, "CC6.1"), criterion.Stream);
    }

    [Fact]
    public void ShouldKeepOnlyOnePendingMutationGivenAnotherIntent()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var intent = Create(edition, draft, operation);
        Assert.True(Propose(owner, intent, Actor, Admission(owner, intent)).IsSuccess);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var second = intent with { OperationId = Uuid.CreateVersion4(), Summary = "Another." };
        var result = Propose(owner, second, Actor, Admission(owner, second));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
        Assert.True(owner.HasPendingChange);
    }

    [Fact]
    public void ShouldPreviewWithoutClaimOrEventGivenAValidStructuralIntent()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var intent = Create(edition, Uuid.CreateVersion4(), operation);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);

        // Act
        var result = owner.PreviewProposal(intent, 1, Actor, Admission(owner, intent), null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseProposalGivenNoCommittedOperationClaim()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var intent = Create(edition, Uuid.CreateVersion4(), operation);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);

        // Act
        var result = owner.Propose(intent, 1, Actor, Admission(owner, intent), null, null!);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseOutcomeGivenNoDecisionEvidence()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var intent = Create(edition, Uuid.CreateVersion4(), operation);
        Assert.True(Propose(owner, intent, Actor, Admission(owner, intent)).IsSuccess);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(scenario.PendingEvents));

        // Act
        var result = owner.Decide(operation, 1, proposal.Metadata.EventId, true, null, null!);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Single(scenario.PendingEvents);
        Assert.True(owner.HasPendingChange);
    }

    [Fact]
    public void ShouldReturnNoNewFactGivenExactRetryAndRefuseChangedIntent()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var intent = Create(edition, draft, operation);
        Assert.True(Propose(owner, intent, Actor, Admission(owner, intent)).IsSuccess);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var exact = Propose(owner, intent, Actor, Admission(owner, intent));
        var changed = intent with { Summary = "Different intent." };
        var conflict = Propose(owner, changed, Actor, Admission(owner, changed));

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.False(conflict.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseForgedAdmissionGivenDifferentTargetOrIntent()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", operation);
        var intent = Create(edition, Uuid.CreateVersion4(), operation);
        var evidence = Admission(owner, intent) with { Target = "stream://bdgrz/other/target" };
        var scenario = new AggregateScenario<CriteriaEntry>(owner);

        // Act
        var result = Propose(owner, intent, Actor, evidence);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldKeepWitnessProtectedGivenAConcurrentRemovalAttempt()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var createId = Uuid.CreateVersion4();
        var attempt = Uuid.CreateVersion4();
        var created = new CriteriaEntry(edition, "CC6.1", createId, attempt);
        var create = Create(edition, draft, createId);
        Assert.True(Propose(created, create, Actor, Admission(created, create)).IsSuccess);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(created).PendingEvents));
        Assert.True(created.Decide(createId, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var started = Started(edition, draft, attempt, 1, created.CurrentIncarnationId);
        Assert.True(created.PinWitness(started).IsSuccess);
        var history = new AggregateScenario<CriteriaEntry>(created).PendingEvents.ToArray();
        var removeId = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", removeId);
        var scenario = new AggregateScenario<CriteriaEntry>(owner).Given(history);
        var remove = new CriterionChangeIntent(edition, draft, "CC6.1", removeId, 1, "remove", null, null, null, null, null);

        // Act
        var result = Propose(owner, remove, Actor, Admission(owner, remove));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.True(owner.IsWitnessPinned);
    }

    [Fact]
    public void ShouldRejectDelayedPinGivenAnEarlierAttemptFence()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var createId = Uuid.CreateVersion4();
        var attempt = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", createId, attempt);
        var create = Create(edition, draft, createId);
        Assert.True(Propose(owner, create, Actor, Admission(owner, create)).IsSuccess);
        var proposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(owner).PendingEvents));
        Assert.True(owner.Decide(createId, 1, proposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var incarnation = owner.CurrentIncarnationId;
        var aborted = Aborted(edition, draft, attempt, 1, incarnation);
        Assert.True(owner.FenceWitness(aborted).IsSuccess);
        var scenario = new AggregateScenario<CriteriaEntry>(owner);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var result = owner.PinWitness(Started(edition, draft, attempt, 1, incarnation));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
        Assert.False(owner.IsWitnessPinned);
    }

    [Fact]
    public void ShouldPreserveEarlierIncarnationGivenARefusedReplacement()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var createId = Uuid.CreateVersion4();
        var created = new CriteriaEntry(edition, "CC6.1", createId);
        var create = Create(edition, draft, createId);
        Assert.True(Propose(created, create, Actor, Admission(created, create)).IsSuccess);
        var firstProposal = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(created).PendingEvents));
        Assert.True(created.Decide(createId, 1, firstProposal.Metadata.EventId, true, null, DraftEvidence()).IsSuccess);
        var incarnation = created.CurrentIncarnationId;
        var replaceId = Uuid.CreateVersion4();
        var owner = new CriteriaEntry(edition, "CC6.1", replaceId);
        new AggregateScenario<CriteriaEntry>(owner).Given(new AggregateScenario<CriteriaEntry>(created).PendingEvents.ToArray());
        var replace = new CriterionChangeIntent(edition, draft, "CC6.1", replaceId, 1, "replace", "criterion", "security", null, null, "Replacement.");
        Assert.True(Propose(owner, replace, Actor, Admission(owner, replace)).IsSuccess);
        var proposed = Assert.IsType<CriterionChangeProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(owner).PendingEvents));

        // Act
        var result = owner.Decide(replaceId, 1, proposed.Metadata.EventId, false, "edition_published", PublishedRefusalEvidence());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(incarnation, owner.CurrentIncarnationId);
        Assert.Equal(1, owner.Revision);
    }

    static CriteriaEntryDecisionEvidence DraftEvidence() => new("draft", Uuid.CreateVersion4(), 0, 1, 2);
    static CriteriaEntryDecisionEvidence PublishedRefusalEvidence() =>
        new("publication", Uuid.CreateVersion4(), 1, 2, 3);

    static Result Propose(CriteriaEntry owner, CriterionChangeIntent intent, ActorAttribution actor,
        AccessAdmissionEvidence admission)
    {
        var claim = new CriterionEntryOperationClaimed(intent, null, 1, actor, admission);
        claim.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion5(intent.OperationId, "claim"),
            CriteriaEntryOperation.IdentityOf(intent.EditionId, intent.Identifier, intent.OperationId), 1, Now));
        return owner.Propose(intent, 1, actor, admission, null, claim);
    }

    static CriterionChangeIntent Create(Uuid edition, Uuid draft, Uuid operation) =>
        new(edition, draft, "CC6.1", operation, 0, "create", "criterion", "security", null, null, "Original product summary.");

    static AccessAdmissionEvidence SummaryAdmission(CriteriaEntry owner, CriterionSummaryIntent intent)
    {
        var source = new AccessAdmissionSourceVersion("stream://bdgrz/authority/source", "v1", 1);
        return new AccessAdmissionEvidence(UserId, Actor.Subject, Actor.Issuer, intent.OperationId, owner.Stream.ToString(),
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1), Now, "policy", source, source)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = source
        };
    }

    static CriterionEntryOperationClaimed SummaryClaim(CriterionSummaryIntent intent, AccessAdmissionEvidence admission)
    {
        var claim = new CriterionEntryOperationClaimed(null, intent, 1, Actor, admission);
        claim.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion5(intent.OperationId, "claim"),
            CriteriaEntryOperation.IdentityOf(intent.EditionId, intent.Identifier, intent.OperationId), 1, Now));
        return claim;
    }

    static AccessAdmissionEvidence Admission(CriteriaEntry owner, CriterionChangeIntent intent)
    {
        var identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/actor", "generation", 1);
        var policy = new AccessAdmissionSourceVersion("stream://bdgrz/policies/current", "generation", 1);
        return new AccessAdmissionEvidence(UserId, Actor.Subject, Actor.Issuer, intent.OperationId,
            owner.Stream.ToString(), CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), 1), Now, "policy-v1", identity, policy)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = new AccessAdmissionSourceVersion("stream://bdgrz/operators/actor", "generation", 1)
        };
    }

    static CriteriaPublicationStarted Started(Uuid edition, Uuid draft, Uuid attempt, long ordinal, Uuid incarnation)
    {
        var fact = new CriteriaPublicationStarted(edition, draft, attempt, 1, ordinal, "CC6.1", incarnation,
            "2017_tsc_2022_pof", 1, Uuid.CreateVersion4(), Actor, null);
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition, 1, Now));
        return fact;
    }

    static CriteriaPublicationAborted Aborted(Uuid edition, Uuid draft, Uuid attempt, long ordinal, Uuid incarnation)
    {
        var fact = new CriteriaPublicationAborted(edition, draft, attempt, ordinal, "CC6.1", incarnation, "abort");
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition, 1, Now));
        return fact;
    }
}
