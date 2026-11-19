using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaValueEditionParticipantVerifierTests
{
    [Fact]
    public async Task ShouldVerifyRenameGivenExactQualifiedParticipantChain()
    {
        // Arrange
        var fixture = new Fixture();
        var verifier = fixture.Verifier();

        // Act
        // Assert
        Assert.True(await verifier.ObserveAsync(fixture.Request, CancellationToken.None));
        Assert.True(await verifier.ObserveAsync(fixture.Renamed, CancellationToken.None));
        Assert.True(await verifier.ObserveAsync(fixture.Completed, CancellationToken.None));
    }

    [Fact]
    public async Task ShouldKeepRenamePendingGivenUnqualifiedCrossAreaCausality()
    {
        // Arrange
        var fixture = new Fixture { Causal = false };
        var verifier = fixture.Verifier();
        Assert.True(await verifier.ObserveAsync(fixture.Request, CancellationToken.None));

        // Act
        var result = await verifier.ObserveAsync(fixture.Renamed, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ShouldKeepRenamePendingGivenMissingReservation()
    {
        // Arrange
        var fixture = new Fixture { ShowReservation = false };
        var verifier = fixture.Verifier();
        Assert.True(await verifier.ObserveAsync(fixture.Request, CancellationToken.None));

        // Act
        var result = await verifier.ObserveAsync(fixture.Renamed, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ShouldKeepRenamePendingGivenUnqualifiedClaimHistory()
    {
        // Arrange
        var fixture = new Fixture { Covered = false };
        var verifier = fixture.Verifier();
        Assert.True(await verifier.ObserveAsync(fixture.Request, CancellationToken.None));
        Assert.True(await verifier.ObserveAsync(fixture.Renamed, CancellationToken.None));

        // Act
        var result = await verifier.ObserveAsync(fixture.Completed, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ShouldKeepCleanupPendingGivenUnobservedWitnessFence()
    {
        // Arrange
        var fixture = new Fixture();
        var verifier = fixture.Verifier();
        var edition = Uuid.CreateVersion4();
        var generation = Uuid.CreateVersion4();
        var attempt = Uuid.CreateVersion4();
        var incarnation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(edition);
        var aborted = DomainEventSeed.Attach(new CriteriaPublicationAborted(edition, generation, attempt, 1,
            "CC6.1", incarnation, "aborted"), owner.Id, 1);
        var cleanup = DomainEventSeed.Attach(new CriteriaPublicationAbortCleanupCompleted(edition, generation,
            attempt, 1, "CC6.1", incarnation, Uuid.CreateVersion4()), owner.Id, 2);
        var abortedRecord = new CriteriaOrderedSourceEvent(owner.Stream, 0, 1, aborted);
        var cleanupRecord = new CriteriaOrderedSourceEvent(owner.Stream, 1, 3, cleanup);
        Assert.True(await verifier.ObserveAsync(abortedRecord, CancellationToken.None));

        // Act
        var result = await verifier.ObserveAsync(cleanupRecord, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ShouldVerifyCleanupGivenExactOrderedWitnessFence()
    {
        // Arrange
        var fixture = new Fixture();
        var verifier = fixture.Verifier();
        var edition = Uuid.CreateVersion4();
        var generation = Uuid.CreateVersion4();
        var attempt = Uuid.CreateVersion4();
        var incarnation = Uuid.CreateVersion4();
        var owner = new CriteriaEdition(edition);
        var witness = new CriteriaEntry(edition, "CC6.1");
        var aborted = DomainEventSeed.Attach(new CriteriaPublicationAborted(edition, generation, attempt, 1,
            "CC6.1", incarnation, "aborted"), owner.Id, 1);
        var fence = DomainEventSeed.Attach(new CriterionWitnessPinFenced(edition, generation, "CC6.1", attempt, 1,
            incarnation, aborted.Metadata.EventId), witness.Id, 1);
        var cleanup = DomainEventSeed.Attach(new CriteriaPublicationAbortCleanupCompleted(edition, generation, attempt, 1,
            "CC6.1", incarnation, fence.Metadata.EventId), owner.Id, 2);
        fixture.Add(new CriteriaOrderedSourceEvent(witness.Stream, 0, 2, fence));
        Assert.True(await verifier.ObserveAsync(new CriteriaOrderedSourceEvent(owner.Stream, 0, 1, aborted), CancellationToken.None));

        // Act
        var result = await verifier.ObserveAsync(new CriteriaOrderedSourceEvent(owner.Stream, 1, 3, cleanup), CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRequireQualifiedClaimHistoryGivenARenameRefusal(bool covered)
    {
        // Arrange
        var fixture = new Fixture { Covered = covered };
        var refusal = fixture.ConfigureRefusal();
        var verifier = fixture.Verifier();
        Assert.True(await verifier.ObserveAsync(fixture.Request, CancellationToken.None));

        // Act
        var result = await verifier.ObserveAsync(refusal, CancellationToken.None);

        // Assert
        Assert.Equal(covered, result);
    }

    sealed class Fixture : ICriteriaExactSourceReader, ICriteriaSourceCutCoverage, ICriteriaParticipantCausalOrder
    {
        readonly List<CriteriaOrderedSourceEvent> _source = [];
        readonly Uuid _reservationId;

        public Fixture()
        {
            var edition = Uuid.CreateVersion4();
            var operation = Uuid.CreateVersion4();
            var actor = new ActorAttribution("operator", "bdgrz");
            var editionOwner = new CriteriaEdition(edition);
            var labelOwner = new EditionLabelClaim("edition_2");
            var request = DomainEventSeed.Attach(new CriteriaEditionRenameRequested(edition, operation, 1,
                "edition_1", 1, "edition_2", actor, null!), editionOwner.Id, 1);
            var reserved = DomainEventSeed.Attach(new EditionLabelReserved("edition_2", edition, operation, 1,
                request.Metadata.EventId, actor), labelOwner.Id, 1);
            var renamed = DomainEventSeed.Attach(new CriteriaEditionDraftRenamed(edition, operation,
                "edition_1", 1, "edition_2", 1, reserved.Metadata.EventId), editionOwner.Id, 2);
            var confirmation = DomainEventSeed.Attach(new EditionLabelConfirmed("edition_2", edition, operation, 1,
                renamed.Metadata.EventId), labelOwner.Id, 2);
            var completed = DomainEventSeed.Attach(new CriteriaEditionRenameCompleted(edition, operation, "edition_2", 1,
                confirmation.Metadata.EventId), editionOwner.Id, 3);
            Request = new CriteriaOrderedSourceEvent(editionOwner.Stream, 0, 1, request);
            Renamed = new CriteriaOrderedSourceEvent(editionOwner.Stream, 1, 2, renamed);
            Completed = new CriteriaOrderedSourceEvent(editionOwner.Stream, 2, 3, completed);
            _reservationId = reserved.Metadata.EventId;
            _source.Add(new CriteriaOrderedSourceEvent(labelOwner.Stream, 0, 10, reserved));
            _source.Add(new CriteriaOrderedSourceEvent(labelOwner.Stream, 1, 11, confirmation));
        }

        public bool Causal { get; init; } = true;
        public bool Covered { get; init; } = true;
        public bool ShowReservation { get; init; } = true;
        public CriteriaOrderedSourceEvent Request { get; }
        public CriteriaOrderedSourceEvent Renamed { get; }
        public CriteriaOrderedSourceEvent Completed { get; }
        public void Add(CriteriaOrderedSourceEvent record) => _source.Add(record);
        public CriteriaOrderedSourceEvent ConfigureRefusal()
        {
            var request = (CriteriaEditionRenameRequested)Request.Event;
            var claim = new EditionLabelClaim(request.NewLabel);
            var holder = DomainEventSeed.Attach(new EditionLabelReserved(request.NewLabel, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), new ActorAttribution("other", "bdgrz")), claim.Id, 1);
            var refused = DomainEventSeed.Attach(new EditionLabelReservationRefused(request.NewLabel, request.EditionId,
                request.OperationId, 2, request.Metadata.EventId, request.Actor, holder.EditionId, holder.OperationId,
                holder.Generation, holder.Metadata.EventId), claim.Id, 2);
            _source.Clear();
            _source.Add(new CriteriaOrderedSourceEvent(claim.Stream, 0, 10, holder));
            _source.Add(new CriteriaOrderedSourceEvent(claim.Stream, 1, 11, refused));
            var outcome = DomainEventSeed.Attach(new CriteriaEditionRenameRefused(request.EditionId, request.OperationId,
                "label already reserved", refused.Metadata.EventId), request.Metadata.AggregateId, 2);
            return new CriteriaOrderedSourceEvent(Request.Stream, 1, 2, outcome);
        }
        public CriteriaValueEditionParticipantVerifier Verifier() => new(this, this, this);
        public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset, ulong cutAreaOffset,
            CancellationToken ct = default) => ValueTask.FromResult(Covered);
        public ValueTask<bool> PrecedesAsync(CriteriaOrderedSourceEvent first, CriteriaOrderedSourceEvent second,
            CancellationToken ct = default) => ValueTask.FromResult(Causal);
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => ValueTask.FromResult(_source.FirstOrDefault(x =>
                x.Stream == stream && x.Event.Metadata.AggregateId == aggregateId && x.Event.Metadata.EventId == eventId &&
                (ShowReservation || x.Event.Metadata.EventId != _reservationId)));
        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var record in _source.Where(x => x.Stream == stream && x.Event.Metadata.AggregateId == aggregateId))
            {
                ct.ThrowIfCancellationRequested();
                yield return record;
            }
            await Task.CompletedTask;
        }
    }
}
