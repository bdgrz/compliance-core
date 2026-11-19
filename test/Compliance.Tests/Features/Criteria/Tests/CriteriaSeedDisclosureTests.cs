using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaSeedDisclosureTests
{
    static readonly string Fingerprint = new('a', 64);
    static readonly ActorAttribution Actor = new("criteria-seed", "bdgrz");

    [Fact]
    public void ShouldDeriveIndependentIdentitiesGivenEditionCategoryAndCode()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var first = new CriteriaSeedDisclosure(edition, "privacy", "points_of_focus_partial");

        // Act
        var repeat = new CriteriaSeedDisclosure(edition, "privacy", "points_of_focus_partial");
        var otherCategory = new CriteriaSeedDisclosure(edition, "security", "points_of_focus_partial");
        var otherCode = new CriteriaSeedDisclosure(edition, "privacy", "privacy_lifecycle_unsupported");
        var otherEdition = new CriteriaSeedDisclosure(Uuid.CreateVersion4(), "privacy", "points_of_focus_partial");

        // Assert
        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(first.Stream, repeat.Stream);
        Assert.NotEqual(first.Id, otherCategory.Id);
        Assert.NotEqual(first.Id, otherCode.Id);
        Assert.NotEqual(first.Id, otherEdition.Id);
        Assert.Equal("bdgrz", first.Stream.Realm);
        Assert.Equal($"criteria-{edition}", first.Stream.Area);
    }

    [Fact]
    public void ShouldRecordOneFactAndNoOpGivenAnExactRetry()
    {
        // Arrange
        var owner = Open();
        var scenario = new AggregateScenario<CriteriaSeedDisclosure>(owner);
        var operationId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();

        // Act
        var first = owner.Record(operationId, "seed-v1", Fingerprint, sourceId, "Partial coverage.", Actor);
        var retry = owner.Record(operationId, "seed-v1", Fingerprint, sourceId, "Partial coverage.", Actor);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        var fact = Assert.IsType<CriteriaSeedDisclosureRecorded>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(operationId, fact.OperationId);
        Assert.Equal(sourceId, fact.EditionRequestEventId);
        Assert.Equal("Partial coverage.", owner.Recorded!.Note);
    }

    [Fact]
    public void ShouldRefuseChangedIntentWithoutFactGivenAnExistingDisclosure()
    {
        // Arrange
        var owner = Open();
        var scenario = new AggregateScenario<CriteriaSeedDisclosure>(owner);
        var operationId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        Assert.True(owner.Record(operationId, "seed-v1", Fingerprint, sourceId, "Partial coverage.", Actor).IsSuccess);

        // Act
        var changedNote = owner.Record(operationId, "seed-v1", Fingerprint, sourceId, "Changed coverage.", Actor);
        var changedVersion = owner.Record(operationId, "seed-v2", Fingerprint, sourceId, "Partial coverage.", Actor);
        var differentOperation = owner.Record(Uuid.CreateVersion4(), "seed-v1", Fingerprint, sourceId,
            "Partial coverage.", Actor);

        // Assert
        Assert.False(changedNote.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changedNote.Error!.Kind);
        Assert.False(changedVersion.IsSuccess);
        Assert.False(differentOperation.IsSuccess);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenAnInvalidSeedBinding()
    {
        // Arrange
        var owner = Open();
        var scenario = new AggregateScenario<CriteriaSeedDisclosure>(owner);

        // Act
        var missingSource = owner.Record(Uuid.CreateVersion4(), "seed-v1", Fingerprint, Uuid.Empty,
            "Partial coverage.", Actor);
        var invalidFingerprint = owner.Record(Uuid.CreateVersion4(), "seed-v1", "not-sha256", Uuid.CreateVersion4(),
            "Partial coverage.", Actor);

        // Assert
        Assert.False(missingSource.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, missingSource.Error!.Kind);
        Assert.False(invalidFingerprint.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecoverExactOutcomeGivenReplayedHistory()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var original = new CriteriaSeedDisclosure(edition, "privacy", "privacy_lifecycle_unsupported");
        var operationId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        Assert.True(original.Record(operationId, "seed-v1", Fingerprint, sourceId, "Privacy is limited.", Actor).IsSuccess);
        var history = NativeFacts.Raised(original);

        // Act
        var replayed = NativeFacts.Replay(
            new CriteriaSeedDisclosure(edition, "privacy", "privacy_lifecycle_unsupported"), history);
        var retry = replayed.Record(operationId, "seed-v1", Fingerprint, sourceId, "Privacy is limited.", Actor);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal("Privacy is limited.", replayed.Recorded!.Note);
        Assert.Single(history);
    }

    [Fact]
    public void ShouldRejectReplayGivenAForeignEditionPayload()
    {
        // Arrange
        var owner = Open();
        var foreign = new CriteriaSeedDisclosureRecorded(Uuid.CreateVersion4(), "privacy",
            "privacy_lifecycle_unsupported", Uuid.CreateVersion4(), "seed-v1", Fingerprint,
            Uuid.CreateVersion4(), "Privacy is limited.", Actor);
        var fact = DomainEventSeed.Attach(foreign, owner.Id, 1);

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => new AggregateScenario<CriteriaSeedDisclosure>(owner).Given(fact));
    }

    [Fact]
    public async Task ShouldCommitOnlyOneFactGivenCompetingEmptyStreamAppends()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var first = new CriteriaSeedDisclosure(edition, "privacy", "privacy_lifecycle_unsupported");
        var second = new CriteriaSeedDisclosure(edition, "privacy", "privacy_lifecycle_unsupported");
        var store = new InMemoryEventStore();
        Assert.True(first.Record(Uuid.CreateVersion4(), "seed-v1", Fingerprint, Uuid.CreateVersion4(),
            "First note.", Actor).IsSuccess);
        Assert.True(second.Record(Uuid.CreateVersion4(), "seed-v1", Fingerprint, Uuid.CreateVersion4(),
            "Second note.", Actor).IsSuccess);
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
        Assert.IsType<CriteriaSeedDisclosureRecorded>(persisted[0].Event);
    }

    static CriteriaSeedDisclosure Open() =>
        new(Uuid.CreateVersion4(), "privacy", "privacy_lifecycle_unsupported");

    static DomainEvent[] AttachedFacts(CriteriaSeedDisclosure owner) =>
        new AggregateScenario<CriteriaSeedDisclosure>(owner).PendingEvents.Select((fact, index) =>
            DomainEventSeed.Attach(fact, owner.Id, (ulong)index + 1)).ToArray();
}
