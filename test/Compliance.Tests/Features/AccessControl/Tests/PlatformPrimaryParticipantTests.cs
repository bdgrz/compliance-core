using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PlatformPrimaryParticipantTests
{
    static readonly ActorAttribution SystemActor = new("portia:system", "Portia");
    static readonly IExecutionContext SystemContext = new RequestDispatchContext(RequestActor.System);

    [Fact]
    public async Task ShouldRecoverExactTargetOutcomeGivenLostAppendAcknowledgement()
    {
        // Arrange
        var fixture = new Fixture();
        var reservation = fixture.ReserveInitial();
        fixture.Store.LoseNextAcknowledgement = true;

        // Act
        var unknown = await fixture.Service.ApplyReservationAsync(reservation.Metadata.EventId, 0, 0, 0, SystemContext);
        var retry = await fixture.Service.ApplyReservationAsync(reservation.Metadata.EventId, 0, 0, 0, SystemContext);
        var changedHead = await fixture.Service.ApplyReservationAsync(reservation.Metadata.EventId, 0, 1, 1, SystemContext);
        var target = Assert.IsType<PlatformOperatorBootstrapGrantAndMarkAccepted>(fixture.Store.At(
            PlatformOperator.AddressOf(reservation.UserId), 0));
        var confirmed = await fixture.Service.RecordOutcomeAsync(reservation.UserId, 0, target.Metadata.EventId,
            0, reservation.Metadata.EventId, SystemContext);

        // Assert
        Assert.False(unknown.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changedHead.IsSuccess);
        Assert.True(confirmed.IsSuccess);
        Assert.IsType<PrimaryPlatformOperatorAppointed>(fixture.Store.At(PrimaryPlatformOperators.Address, 1));
    }

    [Fact]
    public async Task ShouldLeaveTargetUnchangedGivenWrongSourcePosition()
    {
        // Arrange
        var fixture = new Fixture();
        var reservation = fixture.ReserveInitial();

        // Act
        var result = await fixture.Service.ApplyReservationAsync(reservation.Metadata.EventId, 1, 0, 0, SystemContext);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(fixture.Store.At(PlatformOperator.AddressOf(reservation.UserId), 0));
    }

    [Fact]
    public async Task ShouldFenceReservationGivenAnotherCommittedOperationWonItsOriginalPosition()
    {
        // Arrange
        var fixture = new Fixture();
        var reservation = fixture.ReserveInitial();
        var otherOperation = Uuid.CreateVersion4();
        var target = new PlatformOperator(reservation.UserId, otherOperation);
        var scenario = new AggregateScenario<PlatformOperator>(target);
        Assert.True(target.FencePrimaryMark(otherOperation, 0, Uuid.CreateVersion4(), reservation.LineageId,
            reservation.Generation, Uuid.CreateVersion4()).IsSuccess);
        fixture.Store.Append(target.Stream, scenario.PendingEvents);

        // Act
        var recovered = await fixture.Service.ApplyReservationAsync(reservation.Metadata.EventId, 0, 0, 0, SystemContext);
        var fence = Assert.IsType<PlatformOperatorPrimaryMarkFenced>(fixture.Store.At(target.Stream, 1));
        var refused = await fixture.Service.RecordOutcomeAsync(reservation.UserId, 1, fence.Metadata.EventId,
            0, reservation.Metadata.EventId, SystemContext);

        // Assert
        Assert.True(recovered.IsSuccess);
        Assert.True(refused.IsSuccess);
        Assert.IsType<PrimaryPlatformOperatorSeatRefused>(fixture.Store.At(PrimaryPlatformOperators.Address, 1));
    }

    [Fact]
    public async Task ShouldFenceReservationGivenAuditOccupiedItsOriginalPhysicalPosition()
    {
        // Arrange
        var fixture = new Fixture();
        var reservation = fixture.ReserveInitial();
        var audit = new PlatformOperatorPrimaryMarkFenced(reservation.UserId, Uuid.CreateVersion4(), 0,
            Uuid.CreateVersion4(), reservation.LineageId, 1, Uuid.CreateVersion4());
        audit.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), PlatformOperator.IdentityOf(reservation.UserId),
            0, DateTimeOffset.UtcNow, IsAudit: true));
        var target = PlatformOperator.AddressOf(reservation.UserId);
        fixture.Store.Append(target, [audit]);

        // Act
        var result = await fixture.Service.ApplyReservationAsync(reservation.Metadata.EventId, 0, 0, 0, SystemContext);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<PlatformOperatorPrimaryMarkFenced>(fixture.Store.At(target, 1));
    }

    [Fact]
    public async Task ShouldConfirmDevelopmentFirstSeatGivenAlreadyGrantedOperator()
    {
        // Arrange
        var fixture = new Fixture();
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var actor = new ActorAttribution(user.ToString(), "bdgrz");
        var target = new PlatformOperator(user, grant);
        var targetScenario = new AggregateScenario<PlatformOperator>(target);
        Assert.True(target.Grant(grant, 0, user, actor, "initial operator").IsSuccess);
        fixture.Store.Append(target.Stream, targetScenario.PendingEvents);
        var operation = Uuid.CreateVersion4();
        var plan = Uuid.CreateVersion4();
        var seats = new PrimaryPlatformOperators(operation);
        var seatScenario = new AggregateScenario<PrimaryPlatformOperators>(seats);
        Assert.True(seats.ReserveInitialExisting(operation, 0, user, grant, plan, plan, user, actor).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(seatScenario.PendingEvents));
        fixture.Store.Append(seats.Stream, seatScenario.PendingEvents);

        // Act
        var marked = await fixture.Service.ApplyReservationAsync(reservation.Metadata.EventId, 0, 1, 1, SystemContext);
        var mark = Assert.IsType<PlatformOperatorPrimaryMarkAccepted>(fixture.Store.At(target.Stream, 1));
        var confirmed = await fixture.Service.RecordOutcomeAsync(user, 1, mark.Metadata.EventId,
            0, reservation.Metadata.EventId, SystemContext);

        // Assert
        Assert.True(marked.IsSuccess);
        Assert.True(confirmed.IsSuccess);
        Assert.IsType<PrimaryPlatformOperatorAppointed>(fixture.Store.At(PrimaryPlatformOperators.Address, 1));
    }

    [Fact]
    public async Task ShouldVacateReleasedSeatGivenItsExactTargetIsUnmarked()
    {
        // Arrange
        var fixture = new Fixture();
        var first = fixture.ReserveInitial();
        Assert.True((await fixture.Service.ApplyReservationAsync(first.Metadata.EventId, 0, 0, 0, SystemContext)).IsSuccess);
        var firstTarget = fixture.Store.At(PlatformOperator.AddressOf(first.UserId), 0)!;
        Assert.True((await fixture.Service.RecordOutcomeAsync(first.UserId, 0, firstTarget.Metadata.EventId, 0,
            first.Metadata.EventId, SystemContext)).IsSuccess);
        var secondUser = Uuid.CreateVersion4();
        var secondGrant = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var actor = new ActorAttribution(actorId.ToString(), "bdgrz");
        var grantOwner = new PlatformOperator(secondUser, secondGrant);
        var grant = new AggregateScenario<PlatformOperator>(grantOwner);
        Assert.True(grantOwner.Grant(secondGrant, 0, actorId, actor, "rotation").IsSuccess);
        fixture.Store.Append(grantOwner.Stream, grant.PendingEvents);
        var secondOp = Uuid.CreateVersion4();
        var seats = await fixture.Store.HydrateAsync(new PrimaryPlatformOperators(secondOp));
        var pending = new AggregateScenario<PrimaryPlatformOperators>(seats);
        Assert.True(seats.ReservePrimary(secondOp, 2, secondUser, secondGrant, actorId, actor).IsSuccess);
        var second = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(pending.PendingEvents));
        fixture.Store.Append(seats.Stream, pending.PendingEvents);
        Assert.True((await fixture.Service.ApplyReservationAsync(second.Metadata.EventId, 2, 1, 1, SystemContext)).IsSuccess);
        var secondTarget = fixture.Store.At(PlatformOperator.AddressOf(secondUser), 1)!;
        Assert.True((await fixture.Service.RecordOutcomeAsync(secondUser, 1, secondTarget.Metadata.EventId, 2,
            second.Metadata.EventId, SystemContext)).IsSuccess);
        var releaseOp = Uuid.CreateVersion4();
        var releasing = await fixture.Store.HydrateAsync(new PrimaryPlatformOperators(releaseOp));
        var releaseScenario = new AggregateScenario<PrimaryPlatformOperators>(releasing);
        Assert.True(releasing.Release(releaseOp, 4, first.UserId, first.Generation, actorId, actor, "rotation").IsSuccess);
        var release = Assert.IsType<PrimaryPlatformOperatorSeatReleased>(Assert.Single(releaseScenario.PendingEvents));
        fixture.Store.Append(releasing.Stream, releaseScenario.PendingEvents);

        // Act
        var unmark = await fixture.Service.ApplyReleaseAsync(release.Metadata.EventId, 4, 1, 1, SystemContext);
        var target = Assert.IsType<PlatformOperatorPrimaryUnmarked>(fixture.Store.At(PlatformOperator.AddressOf(first.UserId), 1));
        var wrong = await fixture.Service.RecordOutcomeAsync(first.UserId, 1, target.Metadata.EventId, 4,
            Uuid.CreateVersion4(), SystemContext);
        var vacated = await fixture.Service.RecordOutcomeAsync(first.UserId, 1, target.Metadata.EventId, 4,
            release.Metadata.EventId, SystemContext);

        // Assert
        Assert.True(unmark.IsSuccess);
        Assert.False(wrong.IsSuccess);
        Assert.True(vacated.IsSuccess);
        Assert.IsType<PrimaryPlatformOperatorSeatVacated>(fixture.Store.At(PrimaryPlatformOperators.Address, 5));
    }

    sealed class Fixture
    {
        public Memory Store { get; } = new();
        public PlatformPrimaryParticipant Service => new(Store, Store, Store);

        public PrimaryPlatformOperatorSeatReserved ReserveInitial()
        {
            var operation = Uuid.CreateVersion4();
            var owner = new PrimaryPlatformOperators(operation);
            var scenario = new AggregateScenario<PrimaryPlatformOperators>(owner);
            Assert.True(owner.ReserveInitial(operation, 0, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), SystemActor).IsSuccess);
            var fact = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
            Store.Append(owner.Stream, scenario.PendingEvents);
            return fact;
        }
    }

    sealed class Memory : IDomainEventReader, IAggregateReader, IAggregateExecutor
    {
        readonly Dictionary<EventStreamAddress, List<DomainEvent>> _facts = [];
        public bool LoseNextAcknowledgement { get; set; }
        public void Append(EventStreamAddress stream, IReadOnlyList<DomainEvent> facts)
        {
            if (!_facts.TryGetValue(stream, out var list))
                _facts[stream] = list = [];
            list.AddRange(facts);
        }
        public DomainEvent? At(EventStreamAddress stream, int position) =>
            _facts.TryGetValue(stream, out var list) && position < list.Count ? list[position] : null;
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
        {
            ct.ThrowIfCancellationRequested();
            if (_facts.TryGetValue(aggregate.Stream, out var facts))
            {
                new AggregateScenario<T>(aggregate).Given(facts.Where(fact => !fact.Metadata.IsAudit).ToArray());
                typeof(Aggregate).GetProperty(nameof(Aggregate.CommittedStreamPosition))!.SetValue(aggregate, (ulong)facts.Count);
            }
            return ValueTask.FromResult(aggregate);
        }
        public async ValueTask<Result> ExecuteAsync<T>(T aggregate, Func<T, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct = default) where T : Aggregate
        {
            var owner = await HydrateAsync(aggregate, ct);
            var outcome = operation(owner);
            if (outcome.Result.IsSuccess)
                Append(owner.Stream, new AggregateScenario<T>(owner).PendingEvents);
            if (LoseNextAcknowledgement)
            { LoseNextAcknowledgement = false; throw new IOException("Acknowledgement lost."); }
            return outcome.Result;
        }
        public ValueTask<Result<TOut>> ExecuteAsync<T, TOut>(T aggregate, Func<T, AggregateOutcome<TOut>> operation,
            IExecutionContext context, CancellationToken ct = default) where T : Aggregate => throw new NotSupportedException();
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromVersion,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            if (!_facts.TryGetValue(stream, out var list))
                yield break;
            for (var index = fromVersion; index < (ulong)list.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, list[(int)index], index, EventCursor.Start);
            }
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor after,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
