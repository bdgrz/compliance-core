using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaEntryOperationTests
{
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly ActorAttribution Actor = new("operator", "bdgrz");
    static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldClaimOneIntentGivenAnExactRetryWithFreshAdmission()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var intent = new CriterionSummaryIntent(edition, "CC6.1", operation, 2, "summary");
        var owner = new CriteriaEntryOperation(edition, intent.Identifier, operation);
        var scenario = new AggregateScenario<CriteriaEntryOperation>(owner);
        Assert.True(owner.Claim(intent, 1, Actor, Admission(intent, 1)).IsSuccess);

        // Act
        var exact = owner.Claim(intent, 2, Actor, Admission(intent, 2));

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.IsType<CriterionEntryOperationClaimed>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(intent, owner.ClaimFact!.Summary);
        Assert.Equal(1, owner.ClaimFact.ExecutionGeneration);
    }

    [Fact]
    public void ShouldRefuseChangedIntentOrActorGivenAClaimedOperation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var intent = new CriterionSummaryIntent(edition, "CC6.1", operation, 2, "summary");
        var owner = new CriteriaEntryOperation(edition, intent.Identifier, operation);
        var scenario = new AggregateScenario<CriteriaEntryOperation>(owner);
        Assert.True(owner.Claim(intent, 1, Actor, Admission(intent, 1)).IsSuccess);
        var changed = intent with { Summary = "different" };
        var anotherActor = new ActorAttribution("other", "bdgrz");

        // Act
        var changedIntent = owner.Claim(changed, 1, Actor, Admission(changed, 1));
        var changedActor = owner.Claim(intent, 1, anotherActor, Admission(intent, 1, anotherActor));
        var changedUser = owner.Claim(intent, 1, Actor, Admission(intent, 1, userId: Uuid.CreateVersion4()));

        // Assert
        Assert.False(changedIntent.IsSuccess);
        Assert.False(changedActor.IsSuccess);
        Assert.False(changedUser.IsSuccess);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseStructuralReuseGivenASummaryClaim()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var summary = new CriterionSummaryIntent(edition, "CC6.1", operation, 0, "summary");
        var structural = new CriterionChangeIntent(edition, Uuid.CreateVersion4(), "CC6.1", operation, 0,
            "create", "criterion", "security", null, null, "summary");
        var owner = new CriteriaEntryOperation(edition, summary.Identifier, operation);
        var scenario = new AggregateScenario<CriteriaEntryOperation>(owner);
        Assert.True(owner.Claim(summary, 1, Actor, Admission(summary, 1)).IsSuccess);

        // Act
        var conflict = owner.Claim(structural, 1, Actor, Admission(structural, 1));

        // Assert
        Assert.False(conflict.IsSuccess);
        Assert.Single(scenario.PendingEvents);
        Assert.Null(owner.ClaimFact!.Structural);
    }

    [Fact]
    public void ShouldReplayOneClaimGivenACommittedIntent()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var intent = new CriterionSummaryIntent(edition, "CC6.1", operation, 2, "summary");
        var first = new CriteriaEntryOperation(edition, intent.Identifier, operation);
        Assert.True(first.Claim(intent, 1, Actor, Admission(intent, 1)).IsSuccess);
        var replay = new CriteriaEntryOperation(edition, intent.Identifier, operation);
        var scenario = new AggregateScenario<CriteriaEntryOperation>(replay)
            .Given(new AggregateScenario<CriteriaEntryOperation>(first).PendingEvents.ToArray());

        // Act
        var exact = replay.Claim(intent, 1, Actor, Admission(intent, 1));

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.Equal(intent, replay.ClaimFact!.Summary);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldCommitOnlyOneIntentGivenCompetingOperationClaims()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var firstIntent = new CriterionSummaryIntent(edition, "CC6.1", operation, 1, "first");
        var secondIntent = firstIntent with { Summary = "second" };
        var first = new CriteriaEntryOperation(edition, firstIntent.Identifier, operation);
        var second = new CriteriaEntryOperation(edition, secondIntent.Identifier, operation);
        Assert.True(first.Claim(firstIntent, 1, Actor, Admission(firstIntent, 1)).IsSuccess);
        Assert.True(second.Claim(secondIntent, 1, Actor, Admission(secondIntent, 1)).IsSuccess);
        var store = new InMemoryEventStore();
        var firstFacts = new AggregateScenario<CriteriaEntryOperation>(first).PendingEvents.ToArray();
        var secondFacts = new AggregateScenario<CriteriaEntryOperation>(second).PendingEvents.ToArray();

        // Act
        await store.AppendAsync(first.Stream, 0, firstFacts, CancellationToken.None);
        var stale = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(second.Stream, 0, secondFacts, CancellationToken.None));
        var replay = new CriteriaEntryOperation(edition, firstIntent.Identifier, operation);
        new AggregateScenario<CriteriaEntryOperation>(replay).Given(firstFacts);
        var changedRetry = replay.Claim(secondIntent, 1, Actor, Admission(secondIntent, 1));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(stale);
        Assert.False(changedRetry.IsSuccess);
    }

    [Fact]
    public void ShouldRejectAdmissionForAnotherEntryGivenAClaim()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var intent = new CriterionSummaryIntent(edition, "CC6.1", operation, 1, "summary");
        var owner = new CriteriaEntryOperation(edition, intent.Identifier, operation);
        var scenario = new AggregateScenario<CriteriaEntryOperation>(owner);
        var wrongTarget = Admission(intent, 1) with { Target = CriteriaEntry.StreamFor(edition, "CC6.2").ToString() };

        // Act
        var result = owner.Claim(intent, 1, Actor, wrongTarget);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectHumanReuseGivenTheSeedClaimWonTheSharedIdentity()
    {
        // Arrange
        var (intent, binding) = SeedIntent();
        var owner = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        var incarnation = CriteriaEntry.IncarnationOf(intent.EditionId, intent.Identifier, intent.OperationId);
        var scenario = new AggregateScenario<CriteriaEntryOperation>(owner);
        Assert.True(owner.ClaimSeed(intent, 1, incarnation, null, binding).IsSuccess);

        // Act
        var human = owner.Claim(intent, 1, Actor, Admission(intent, 1));
        var sameSeed = owner.ClaimSeed(intent, 2, incarnation, null, binding);

        // Assert
        Assert.False(human.IsSuccess);
        Assert.True(sameSeed.IsSuccess);
        Assert.Null(owner.ClaimFact);
        Assert.IsType<CriterionEntryOperationSeedClaimed>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(1, owner.SeedClaimFact!.ExecutionGeneration);
    }

    [Fact]
    public void ShouldRejectSeedClaimGivenTheEditionBindingNamesAnotherFramework()
    {
        // Arrange
        var (intent, binding) = SeedIntent();
        var owner = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        var scenario = new AggregateScenario<CriteriaEntryOperation>(owner);
        var incarnation = CriteriaEntry.IncarnationOf(intent.EditionId, intent.Identifier, intent.OperationId);

        // Act
        var result = owner.ClaimSeed(intent, 1, incarnation, null, binding with { Framework = "other" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldCommitOnlyOneClaimGivenConcurrentHumanAndSeedIntents()
    {
        // Arrange
        var (intent, binding) = SeedIntent();
        var seed = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        var human = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        var incarnation = CriteriaEntry.IncarnationOf(intent.EditionId, intent.Identifier, intent.OperationId);
        Assert.True(seed.ClaimSeed(intent, 1, incarnation, null, binding).IsSuccess);
        Assert.True(human.Claim(intent, 1, Actor, Admission(intent, 1)).IsSuccess);
        var seedFacts = new AggregateScenario<CriteriaEntryOperation>(seed).PendingEvents.ToArray();
        var humanFacts = new AggregateScenario<CriteriaEntryOperation>(human).PendingEvents.ToArray();
        var store = new InMemoryEventStore();

        // Act
        await store.AppendAsync(seed.Stream, 0, seedFacts, CancellationToken.None);
        var lost = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(human.Stream, 0, humanFacts, CancellationToken.None));
        var replay = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        new AggregateScenario<CriteriaEntryOperation>(replay).Given(seedFacts);
        var changed = replay.Claim(intent, 1, Actor, Admission(intent, 1));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(lost);
        Assert.False(changed.IsSuccess);
    }

    static (CriterionChangeIntent Intent, CriteriaEditionDraftRequested Binding) SeedIntent()
    {
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First(item => item.Kind == "criterion");
        var draft = PlatformCriteriaSeedDefinition.OperationOf(edition.EditionId, "edition");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(edition.EditionId, "entry:" + entry.Identifier);
        var intent = new CriterionChangeIntent(edition.EditionId, draft, entry.Identifier, operation, 0,
            "create", entry.Kind, entry.Category, null, null, entry.Summary);
        var fingerprint = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var binding = new CriteriaEditionDraftRequested(edition.EditionId, draft, draft, 1,
            "tsc", edition.EditionLabel, new ActorAttribution("criteria-seed-bootstrap", "system"), null,
            CriteriaSeedFingerprint.Version, fingerprint);
        binding.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.EditionId, 1, Now));
        return (intent, binding);
    }

    static AccessAdmissionEvidence Admission<T>(T intent, long generation, ActorAttribution? actor = null,
        Uuid userId = default) where T : notnull
    {
        var (edition, identifier, operation) = intent switch
        {
            CriterionChangeIntent structural => (structural.EditionId, structural.Identifier, structural.OperationId),
            CriterionSummaryIntent summary => (summary.EditionId, summary.Identifier, summary.OperationId),
            _ => throw new InvalidOperationException("Unexpected criteria intent."),
        };
        var selectedActor = actor ?? Actor;
        var identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/actor", "generation", 1);
        var policy = new AccessAdmissionSourceVersion("stream://bdgrz/policies/current", "generation", 1);
        return new AccessAdmissionEvidence(userId == Uuid.Empty ? UserId : userId, selectedActor.Subject,
            selectedActor.Issuer, operation, CriteriaEntry.StreamFor(edition, identifier).ToString(),
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), generation), Now, "policy-v1",
            identity, policy)
        {
            FormatVersion = 1,
            PolicyContentSha256 = new string('a', 64),
            Operator = new AccessAdmissionSourceVersion("stream://bdgrz/operators/actor", "generation", 1),
        };
    }
}
