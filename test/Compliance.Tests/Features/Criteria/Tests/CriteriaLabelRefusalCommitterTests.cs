using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaLabelRefusalCommitterTests
{
    [Fact]
    public async Task ShouldStayPendingGivenNoQualifiedClaimHead()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var committer = new CriteriaLabelRefusalCommitter(fixture.Events, fixture.Reader, fixture.Executor);

        // Act
        var result = await fixture.RefuseAsync(committer);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Empty(await fixture.RefusalsAsync());
    }

    [Fact]
    public async Task ShouldStayPendingGivenHeadCertificateRejectsObservedPosition()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var head = new Head(false);
        var committer = new CriteriaLabelRefusalCommitter(fixture.Events, fixture.Reader, fixture.Executor, head);

        // Act
        var result = await fixture.RefuseAsync(committer);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(1UL, head.LastPosition);
        Assert.Empty(await fixture.RefusalsAsync());
    }

    [Fact]
    public async Task ShouldCommitAndReplayOneExactRefusalGivenQualifiedHead()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var committer = new CriteriaLabelRefusalCommitter(fixture.Events, fixture.Reader, fixture.Executor, new Head(true));

        // Act
        var first = await fixture.RefuseAsync(committer);
        var retry = await fixture.RefuseAsync(committer);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        var fact = Assert.Single(await fixture.RefusalsAsync());
        Assert.Equal(fixture.Request.Metadata.EventId, fact.EditionRequestEventId);
        Assert.Equal(fixture.Holder.Metadata.EventId, fact.HolderReservationEventId);
        Assert.Equal(2, fact.RequestedGeneration);
    }

    [Fact]
    public async Task ShouldReturnPendingThenReplayGivenTheAppendAcknowledgementWasLost()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var uncertain = new LoseFirstAcknowledgement(fixture.Executor);
        var committer = new CriteriaLabelRefusalCommitter(fixture.Events, fixture.Reader, uncertain, new Head(true));

        // Act
        var first = await fixture.RefuseAsync(committer);
        var retry = await fixture.RefuseAsync(committer);

        // Assert
        Assert.False(first.IsSuccess);
        Assert.True(first.Error!.IsTransient);
        Assert.True(retry.IsSuccess);
        Assert.Single(await fixture.RefusalsAsync());
    }

    [Fact]
    public async Task ShouldReplayOriginalGenerationGivenAnotherHolderAcquiredAfterRefusal()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var committer = new CriteriaLabelRefusalCommitter(fixture.Events, fixture.Reader, fixture.Executor, new Head(true));
        Assert.True((await fixture.RefuseAsync(committer)).IsSuccess);
        var holder = fixture.Holder;
        var release = await fixture.Executor.ExecuteAsync(new EditionLabelClaim("edition_1", holder.EditionId, holder.OperationId),
            owner => AggregateOutcome.CommitOnSuccess(owner.Release(holder.EditionId, holder.OperationId, 1,
                Uuid.CreateVersion4(), "cancelled")), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(release.IsSuccess);
        var successorEdition = Uuid.CreateVersion4();
        var successorOperation = Uuid.CreateVersion4();
        var reserve = await fixture.Executor.ExecuteAsync(new EditionLabelClaim("edition_1", successorEdition, successorOperation),
            owner =>
            {
                var result = owner.Reserve(successorEdition, successorOperation, 2, Uuid.CreateVersion4(), fixture.Request.Actor);
                return result.IsSuccess ? AggregateOutcome.Commit(Result.Success) : AggregateOutcome.Discard(Result.Failure(result.Error!));
            }, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(reserve.IsSuccess);

        // Act
        var retry = await fixture.RefuseAsync(committer);

        // Assert
        Assert.True(retry.IsSuccess);
        var refusal = Assert.Single(await fixture.RefusalsAsync());
        Assert.Equal(2, refusal.RequestedGeneration);
        var current = await fixture.Reader.HydrateAsync(new EditionLabelClaim("edition_1"));
        Assert.True(current.IsHeldBy(successorEdition, successorOperation, 2));
    }

    [Fact]
    public async Task ShouldRemainPendingGivenHolderReleaseWinsAfterHeadObservation()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var holder = fixture.Holder;
        var racing = new InterleavingExecutor(fixture.Executor, async () =>
        {
            var released = await fixture.Executor.ExecuteAsync(new EditionLabelClaim("edition_1", holder.EditionId, holder.OperationId),
                owner => AggregateOutcome.CommitOnSuccess(owner.Release(holder.EditionId, holder.OperationId, 1,
                    Uuid.CreateVersion4(), "cancelled")), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            Assert.True(released.IsSuccess);
        });
        var committer = new CriteriaLabelRefusalCommitter(fixture.Events, fixture.Reader, racing, new Head(true));

        // Act
        var result = await fixture.RefuseAsync(committer);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Empty(await fixture.RefusalsAsync());
    }

    sealed class Head(bool answer) : ICriteriaFreshLabelClaimHead
    {
        public ulong LastPosition { get; private set; }
        public ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
            CancellationToken ct = default)
        {
            LastPosition = nextResourceOffset;
            return ValueTask.FromResult(answer);
        }
    }

    sealed class LoseFirstAcknowledgement(IAggregateExecutor inner) : IAggregateExecutor
    {
        int _calls;
        public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct) where TAggregate : Aggregate
        {
            var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
            if (Interlocked.Increment(ref _calls) == 1)
                throw new TimeoutException("The committed append response was lost.");
            return result;
        }
        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);
    }

    sealed class InterleavingExecutor(IAggregateExecutor inner, Func<Task> before) : IAggregateExecutor
    {
        public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate, Func<TAggregate, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct) where TAggregate : Aggregate
        {
            await before();
            return await inner.ExecuteAsync(aggregate, operation, context, ct);
        }
        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _services;
        readonly AsyncServiceScope _scope;
        Fixture(InMemoryEventStore events, ServiceProvider services, AsyncServiceScope scope,
            CriteriaEditionDraftRequested request, EditionLabelReserved holder)
        {
            Events = events;
            _services = services;
            _scope = scope;
            Request = request;
            Holder = holder;
            Reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }
        public InMemoryEventStore Events { get; }
        public IAggregateReader Reader { get; }
        public IAggregateExecutor Executor { get; }
        public CriteriaEditionDraftRequested Request { get; }
        public EditionLabelReserved Holder { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var events = new InMemoryEventStore();
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(events);
            services.AddPortia();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var edition = new CriteriaEdition(Uuid.CreateVersion4());
            var operation = Uuid.CreateVersion4();
            var actor = new ActorAttribution("operator", "bdgrz");
            var request = new CriteriaEditionDraftRequested(edition.Id, operation, operation, 1, "tsc", "edition_1", actor, null, null, null);
            request.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
            await events.AppendAsync(edition.Stream, 0, [request]);
            var claim = new EditionLabelClaim("edition_1");
            var holder = new EditionLabelReserved("edition_1", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), actor);
            holder.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 1, DateTimeOffset.UtcNow));
            await events.AppendAsync(claim.Stream, 0, [holder]);
            return new Fixture(events, provider, scope, request, holder);
        }
        public ValueTask<Result> RefuseAsync(CriteriaLabelRefusalCommitter committer) => committer.RefuseIfContestedAsync(
            "edition_1", Request.EditionId, Request.OperationId, Request.Metadata.EventId, Request.Actor,
            new RequestDispatchContext(RequestActor.System));
        public async Task<IReadOnlyList<EditionLabelReservationRefused>> RefusalsAsync()
        {
            var refusals = new List<EditionLabelReservationRefused>();
            await foreach (var record in Events.ReadAsync(EditionLabelClaim.AddressOf("edition_1"), 0, CancellationToken.None))
                if (record.Event is EditionLabelReservationRefused refusal)
                    refusals.Add(refusal);
            return refusals;
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
        }
    }
}
