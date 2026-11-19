using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaSeedMetadataTests
{
    static readonly string Fingerprint = new('a', 64);
    static readonly ActorAttribution Actor = new("criteria-seed", "bdgrz");
    static readonly DateTimeOffset PublishedAt = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldUseOneIndependentIdentityGivenAnEdition()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var first = new CriteriaSeedMetadata(edition);

        // Act
        var same = new CriteriaSeedMetadata(edition);
        var other = new CriteriaSeedMetadata(Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Id, same.Id);
        Assert.Equal(first.Stream, same.Stream);
        Assert.NotEqual(first.Id, other.Id);
        Assert.Equal("bdgrz", first.Stream.Realm);
        Assert.Equal($"criteria-{edition}", first.Stream.Area);
        Assert.Equal($"seed-metadata-{edition}", first.Stream.Resource);
    }

    [Fact]
    public void ShouldRecordOneFactAndNoOpGivenExactRetry()
    {
        // Arrange
        var owner = new CriteriaSeedMetadata(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<CriteriaSeedMetadata>(owner);
        var operation = Uuid.CreateVersion4();
        var source = Uuid.CreateVersion4();

        // Act
        var first = Record(owner, operation, source);
        var retry = Record(owner, operation, source);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        var fact = Assert.IsType<CriteriaSeedMetadataRecorded>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(source, fact.EditionRequestEventId);
        Assert.Equal(PublishedAt, fact.PublishedAt);
        Assert.True(fact.NumberedCriteriaComplete);
        Assert.Equal("Partial points of focus.", owner.Recorded!.CoverageNote);
    }

    [Fact]
    public void ShouldRejectChangedIntentWithoutFactGivenExistingMetadata()
    {
        // Arrange
        var owner = new CriteriaSeedMetadata(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<CriteriaSeedMetadata>(owner);
        var operation = Uuid.CreateVersion4();
        var source = Uuid.CreateVersion4();
        Assert.True(Record(owner, operation, source).IsSuccess);

        // Act
        var changed = Record(owner, operation, source, coverage: "Changed coverage.");
        var anotherSource = Record(owner, operation, Uuid.CreateVersion4());
        var anotherOperation = Record(owner, Uuid.CreateVersion4(), source);
        var anotherActor = owner.Record(operation, "seed-v1", Fingerprint, source, "tsc", PublishedAt,
            "Partial points of focus.", "https://example.test/criteria", "identifiers_and_original_summaries",
            true, new ActorAttribution("different-seed", "bdgrz"));

        // Assert
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.False(anotherSource.IsSuccess);
        Assert.False(anotherOperation.IsSuccess);
        Assert.False(anotherActor.IsSuccess);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenInvalidSeedBinding()
    {
        // Arrange
        var owner = new CriteriaSeedMetadata(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<CriteriaSeedMetadata>(owner);

        // Act
        var noSource = Record(owner, Uuid.CreateVersion4(), Uuid.Empty);
        var malformed = owner.Record(Uuid.CreateVersion4(), "seed-v1", "not-sha256", Uuid.CreateVersion4(),
            "tsc", PublishedAt, "Partial points of focus.", "https://example.test/criteria",
            "identifiers_and_original_summaries", true, Actor);

        // Assert
        Assert.False(noSource.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, noSource.Error!.Kind);
        Assert.False(malformed.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecoverExactOutcomeGivenReplayedMetadata()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var original = new CriteriaSeedMetadata(edition);
        var operation = Uuid.CreateVersion4();
        var source = Uuid.CreateVersion4();
        Assert.True(Record(original, operation, source).IsSuccess);
        var history = NativeFacts.Raised(original);

        // Act
        var replayed = NativeFacts.Replay(new CriteriaSeedMetadata(edition), history);
        var retry = Record(replayed, operation, source);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(source, replayed.Recorded!.EditionRequestEventId);
        Assert.Single(history);
    }

    [Fact]
    public void ShouldRejectForeignEditionGivenPersistedEventAddressedToThisOwner()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var owner = new CriteriaSeedMetadata(edition);
        var forged = new CriteriaSeedMetadataRecorded(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "seed-v1",
            Fingerprint, Uuid.CreateVersion4(), "tsc", PublishedAt, "Partial points of focus.",
            "https://example.test/criteria", "identifiers_and_original_summaries", true, Actor);
        var persisted = DomainEventSeed.Attach(forged, owner.Id, 1);

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() =>
            new AggregateScenario<CriteriaSeedMetadata>(owner).Given(persisted));
    }

    [Fact]
    public async Task ShouldCommitOnlyOneMetadataFactGivenCompetingAppends()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var first = new CriteriaSeedMetadata(edition);
        var second = new CriteriaSeedMetadata(edition);
        var store = new InMemoryEventStore();
        Assert.True(Record(first, Uuid.CreateVersion4(), Uuid.CreateVersion4()).IsSuccess);
        Assert.True(Record(second, Uuid.CreateVersion4(), Uuid.CreateVersion4()).IsSuccess);
        var firstFacts = AttachedFacts(first);
        var secondFacts = AttachedFacts(second);

        // Act
        await store.AppendAsync(first.Stream, 0, firstFacts, CancellationToken.None);
        var failure = await Xunit.Record.ExceptionAsync(async () =>
            await store.AppendAsync(second.Stream, 0, secondFacts, CancellationToken.None));

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
        var persisted = new List<DomainEventRecord>();
        await foreach (var record in store.ReadAsync(first.Stream, 0, CancellationToken.None))
            persisted.Add(record);
        Assert.Single(persisted);
        Assert.IsType<CriteriaSeedMetadataRecorded>(persisted[0].Event);
    }

    static Result Record(CriteriaSeedMetadata owner, Uuid operation, Uuid source,
        string coverage = "Partial points of focus.") =>
        owner.Record(operation, "seed-v1", Fingerprint, source, "tsc", PublishedAt, coverage,
            "https://example.test/criteria", "identifiers_and_original_summaries", true, Actor);

    static DomainEvent[] AttachedFacts(CriteriaSeedMetadata owner) =>
        new AggregateScenario<CriteriaSeedMetadata>(owner).PendingEvents.Select((fact, index) =>
            DomainEventSeed.Attach(fact, owner.Id, (ulong)index + 1)).ToArray();
}
