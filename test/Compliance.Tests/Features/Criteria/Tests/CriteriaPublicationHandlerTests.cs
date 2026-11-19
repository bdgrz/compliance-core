using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaPublicationHandlerTests
{
    [Fact]
    public void ShouldSettleOnlyTheExactAbortedWitnessFenceGivenLatePinRetry()
    {
        // Arrange
        var fixture = new Fixture();
        var start = fixture.Started;
        var abort = new CriteriaPublicationAborted(start.EditionId, start.DraftGeneration, start.PublicationAttemptId,
            start.AttemptOrdinal, start.WitnessIdentifier, start.WitnessIncarnationId, "witness refused");
        abort.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), start.EditionId, 2, DateTimeOffset.UtcNow));
        var fence = new CriterionWitnessPinFenced(start.EditionId, start.DraftGeneration, start.WitnessIdentifier,
            start.PublicationAttemptId, start.AttemptOrdinal, start.WitnessIncarnationId, abort.Metadata.EventId);

        // Act and Assert
        Assert.True(PinCriteriaPublicationWitnessHandler.MatchesFence(start, fence, abort));
        Assert.False(PinCriteriaPublicationWitnessHandler.MatchesFence(start, fence with { AttemptOrdinal = 2 }, abort));
        Assert.False(PinCriteriaPublicationWitnessHandler.MatchesFence(start, fence with { PublicationAbortedEventId = Uuid.CreateVersion4() }, abort));
    }
    [Fact]
    public async Task ShouldRemainPendingGivenTheParticipantHeadsAreUnqualified()
    {
        // Arrange
        var fixture = new Fixture();
        var handler = new CompleteCriteriaPublicationHandler(null!, new EmptyAggregateReader(), null!, fixture);
        var context = new RequestContext<CompleteCriteriaPublication>(fixture.Command, RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectGivenPinNamesAnotherAttempt()
    {
        // Arrange
        var fixture = new Fixture();
        var handler = new CompleteCriteriaPublicationHandler(null!, new EmptyAggregateReader(), null!, fixture);
        var different = fixture.Command with { AttemptId = Uuid.CreateVersion4() };
        var context = new RequestContext<CompleteCriteriaPublication>(different, RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRejectGivenWitnessPinPrecedesPublicationStart()
    {
        // Arrange
        var fixture = new Fixture(pinArea: 9);
        var handler = new CompleteCriteriaPublicationHandler(null!, new EmptyAggregateReader(), null!, fixture);
        var context = new RequestContext<CompleteCriteriaPublication>(fixture.Command, RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldKeepWitnessRefusalPendingGivenNoQualifiedEntryHead()
    {
        // Arrange
        var fixture = new Fixture();
        var committer = new CriteriaWitnessRefusalCommitter(null!, null!, null!);

        // Act
        var result = await committer.RefuseAsync(fixture.Started, new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
    }

    sealed class EmptyAggregateReader : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate => ValueTask.FromResult(aggregate);
    }

    sealed class Fixture : ICriteriaExactSourceReader
    {
        readonly CriteriaOrderedSourceEvent _start;
        readonly CriteriaOrderedSourceEvent _pin;
        public Fixture(ulong pinArea = 11)
        {
            var editionId = Uuid.CreateVersion4();
            var attempt = Uuid.CreateVersion4();
            var draft = Uuid.CreateVersion4();
            var incarnation = Uuid.CreateVersion4();
            var edition = new CriteriaEdition(editionId);
            Started = new CriteriaPublicationStarted(editionId, draft, attempt, 1, 1, "CC6.1", incarnation,
                "edition_1", 1, Uuid.CreateVersion4(), new ActorAttribution("operator", "bdgrz"), null);
            Started.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
            _start = new CriteriaOrderedSourceEvent(edition.Stream, 0, 10, Started);
            var witness = new CriteriaEntry(editionId, "CC6.1");
            var pin = new CriterionWitnessPinned(editionId, draft, "CC6.1", attempt, 1, incarnation, Started.Metadata.EventId);
            pin.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), witness.Id, 1, DateTimeOffset.UtcNow));
            _pin = new CriteriaOrderedSourceEvent(witness.Stream, 0, pinArea, pin);
            Command = new CompleteCriteriaPublication(editionId, attempt, "CC6.1", 0, pin.Metadata.EventId);
        }
        public CriteriaPublicationStarted Started { get; }
        public CompleteCriteriaPublication Command { get; }
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId, Uuid eventId,
            CancellationToken ct = default) => ValueTask.FromResult<CriteriaOrderedSourceEvent?>(
                stream == _start.Stream && eventId == _start.Event.Metadata.EventId ? _start :
                stream == _pin.Stream && eventId == _pin.Event.Metadata.EventId ? _pin : null);
        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
