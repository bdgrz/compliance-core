using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

public sealed class NativeOperationExecutorTests
{
    static readonly Uuid Probe = Uuid.Parse("00000000-0000-4000-8000-0000000000b1", CultureInfo.InvariantCulture);
    static readonly Uuid Logical = Uuid.Parse("00000000-0000-4000-8000-0000000000c1", CultureInfo.InvariantCulture);
    static readonly ActorReference Actor = new("member", "m1", "Member One");
    static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly EventStreamAddress Owner = AcceptanceProbe.StreamOf(Probe);
    static readonly ImmutableContentRef Body = new(Uuid.CreateVersion4(), new string('b', 64), 2, "application/json",
        "unit.v1");

    [Fact]
    public async Task ShouldReturnOriginalResponseGivenExactRetryAfterLaterEdit()
    {
        // Arrange
        var (provider, events, _) = Build();
        var first = await ExecuteAsync(provider, Provenance(), 200, "first");
        await EditAsync(provider, "edited later");
        var before = await CountAsync(events);

        // Act
        var retry = await ExecuteAsync(provider, Provenance(At.AddMinutes(5)), 500, "retry candidate");

        // Assert
        var committed = Assert.IsType<AcceptedAppendResult.Committed>(first.Value);
        var replayed = Assert.IsType<AcceptedAppendResult.Replayed>(retry.Value);
        Assert.Equal(committed.Response, replayed.Response);
        Assert.Equal(200, replayed.Response.HttpStatus);
        Assert.Equal(committed.FirstFact, replayed.FirstFact);
        Assert.Equal(before, await CountAsync(events));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("kind")]
    [InlineData("episode")]
    [InlineData("digest")]
    [InlineData("revision")]
    [InlineData("affected")]
    [InlineData("counter")]
    public async Task ShouldConflictWithoutAppendGivenSameLogicalIdWithChangedComparisonField(string field)
    {
        // Arrange
        var (provider, events, kv) = Build();
        await ExecuteAsync(provider, Provenance(), 200, "first");
        var before = await CountAsync(events);
        var content = kv.Snapshot(ContentRoute).Count;
        var other = new EventStreamAddress(AcceptanceProbe.Realm, "probes", "other");
        var changed = field switch
        {
            "actor" => Provenance() with { Actor = new ActorReference("member", "m2", "Member Two") },
            "kind" => Provenance() with { CommandKind = "Revise" },
            "episode" => Provenance() with { Episode = "second" },
            "digest" => Provenance() with { AuthoredIntentSha256 = new string('e', 64) },
            "revision" => Provenance() with { OriginalExpectedRevision = 1 },
            "affected" => Provenance() with { AffectedOwner = other },
            _ => Provenance() with { ExpectedRevisionOwner = other },
        };

        // Act
        var result = await ExecuteAsync(provider, changed, 200, "second");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<AcceptedAppendResult.Conflict>(result.Value);
        Assert.Equal(before, await CountAsync(events));
        Assert.Equal(content, kv.Snapshot(ContentRoute).Count);
    }

    [Fact]
    public async Task ShouldReturnWinnersResponseGivenEqualIntentLosesAppendRace()
    {
        // Arrange
        ServiceProvider? competitorProvider = null;
        var (provider, events, _) = Build(inner => new RaceOnceExecutor(inner, async () =>
        {
            // The identical concurrent request commits its own candidate between our hydration and our append.
            await using var scope = competitorProvider!.CreateAsyncScope();
            var winner = new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                inner, scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
            await winner.ExecuteAsync(Operation(Provenance(), 200, "winner"), Context(), CancellationToken.None);
        }));
        competitorProvider = provider;

        // Act
        var loser = await ExecuteAsync(provider, Provenance(), 200, "loser");

        // Assert
        var replayed = Assert.IsType<AcceptedAppendResult.Replayed>(loser.Value);
        Assert.Equal("winner", Assert.IsType<StandardResponseBody>(replayed.Response.Body).Content.Schema);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldReturnConflictGivenDifferentIntentCommitsBeforeExecutorHydration()
    {
        // Arrange
        ServiceProvider? competitorProvider = null;
        var (provider, events, _) = Build(inner => new CompeteBeforeHydrationExecutor(inner, async () =>
        {
            await using var scope = competitorProvider!.CreateAsyncScope();
            var winner = new NativeOperationExecutor(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                inner, scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
            await winner.ExecuteAsync(Operation(Provenance(), 200, "winner"), Context(), CancellationToken.None);
        }));
        competitorProvider = provider;
        var different = Provenance() with { AuthoredIntentSha256 = new string('e', 64) };

        // Act
        var result = await ExecuteAsync(provider, different, 200, "loser");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<AcceptedAppendResult.Conflict>(result.Value);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldResumeByLogicalIdWithoutDuplicateAppendGivenUnknownAppendOutcome()
    {
        // Arrange
        var (provider, events, _) = Build(inner => new CommitThenFailOnceExecutor(inner));

        // Act
        var result = await ExecuteAsync(provider, Provenance(), 200, "first");

        // Assert
        var replayed = Assert.IsType<AcceptedAppendResult.Replayed>(result.Value);
        Assert.Equal(200, replayed.Response.HttpStatus);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldSurfaceConcurrencyGivenEveryAttemptLosesTheRace()
    {
        // Arrange
        var (provider, events, _) = Build(inner => new AlwaysConflictExecutor());

        // Act
        var failure = await Record.ExceptionAsync(() => ExecuteAsync(provider, Provenance(), 200, "first").AsTask());

        // Assert
        Assert.IsType<EventStreamConcurrencyException>(failure);
        Assert.Equal(0, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendOrFrozenContentGivenGuardFailure()
    {
        // Arrange
        var (provider, events, kv) = Build();
        await EditAsync(provider, "moved on");

        // Act
        var result = await ExecuteAsync(provider, Provenance(), 200, "first", expectedRevision: 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(1, await CountAsync(events));
        Assert.Empty(kv.Snapshot(ContentRoute));
    }

    [Fact]
    public async Task ShouldFailClosedWithoutAppendGivenCorruptFirstResponseContent()
    {
        // Arrange
        var (provider, events, _) = Build(content: store => new CorruptReadStore(store));
        await ExecuteAsync(provider, Provenance(), 200, "first");

        // Act
        var failure = await Record.ExceptionAsync(() => ExecuteAsync(provider, Provenance(), 200, "retry").AsTask());

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(1, await CountAsync(events));
    }

    [Fact]
    public async Task ShouldReportExclusiveAppendFrontierGivenCommittedBatch()
    {
        // Arrange
        var (provider, _, _) = Build();
        await EditAsync(provider, "one");
        await EditAsync(provider, "two");

        // Act
        var result = await ExecuteAsync(provider, Provenance() with { OriginalExpectedRevision = 2 }, 202, "first",
            expectedRevision: 2);

        // Assert
        var committed = Assert.IsType<AcceptedAppendResult.Committed>(result.Value);
        Assert.Equal(3UL, committed.NextPhysicalPositionExclusive);
        Assert.Equal(3UL, committed.FirstFact.AggregateVersion);
        Assert.Equal(Owner, committed.FirstFact.Stream);
        Assert.Equal(202, committed.Response.HttpStatus);
    }

    const string ContentRoute = "kv://bdgrz/immutable-content/tenant-a-";

    static OperationProvenance Provenance(DateTimeOffset? acceptedAt = null) => new(Logical, Logical, null, "Admit",
        Actor, Owner, "initial", 0, new string('d', 64), acceptedAt ?? At, Owner, Owner);

    static NativeOperation<AcceptanceProbe> Operation(OperationProvenance provenance, int status, string payload,
        ulong expectedRevision = 0) => new(AcceptedOperationBinding.Of(provenance),
        () => new AcceptanceProbe(Probe, Logical), probe => probe.Acceptance,
        probe => probe.Version == expectedRevision
            ? Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(status, "application/json", "unit.v1",
                new StandardResponseBody(Body with { Schema = payload })))
            : Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The probe revision changed.", false)),
        (probe, frozen) => probe.Admit(provenance, frozen, payload, expectedRevision));

    static async ValueTask<Result<AcceptedAppendResult>> ExecuteAsync(ServiceProvider provider,
        OperationProvenance provenance, int status, string payload, ulong expectedRevision = 0)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NativeOperationExecutor>()
            .ExecuteAsync(Operation(provenance, status, payload, expectedRevision), Context(), CancellationToken.None);
    }

    static async Task EditAsync(ServiceProvider provider, string payload)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
            new AcceptanceProbe(Probe), probe => AggregateOutcome.CommitOnSuccess(probe.Edit(payload)), Context(),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    static async Task<int> CountAsync(InMemoryEventStore events)
    {
        var count = 0;
        try
        {
            await foreach (var _ in events.ReadAsync(Owner, 0, CancellationToken.None))
                count++;
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return count;
    }

    static RequestDispatchContext Context() => new(RequestActor.System);

    static (ServiceProvider Provider, InMemoryEventStore Events, InMemoryKvClient Kv) Build(
        Func<IAggregateExecutor, IAggregateExecutor>? executor = null,
        Func<IImmutableContentStore, IImmutableContentStore>? content = null)
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        var kv = new InMemoryKvClient();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        IImmutableContentStore store = new FitzKvImmutableContentStore(new PersistenceKvClient(kv));
        services.AddSingleton(content?.Invoke(store) ?? store);
        services.AddScoped(provider => new NativeOperationExecutor(provider.GetRequiredService<IAggregateReader>(),
            executor?.Invoke(provider.GetRequiredService<IAggregateExecutor>()) ??
            provider.GetRequiredService<IAggregateExecutor>(), provider.GetRequiredService<IImmutableContentStore>()));
        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }), events, kv);
    }

    /// <summary>Runs a competing commit, then reports the first attempt as a lost append race.</summary>
    sealed class RaceOnceExecutor(IAggregateExecutor inner, Func<Task> competitor) : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                await competitor();
                throw new EventStreamConcurrencyException("Lost the append race.", new InvalidOperationException());
            }

            return await inner.ExecuteAsync(aggregate, operation, context, ct);
        }
    }

    /// <summary>Lets another operation commit before the real executor hydrates, without forcing an OCC error.</summary>
    sealed class CompeteBeforeHydrationExecutor(IAggregateExecutor inner, Func<Task> competitor) : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            if (Interlocked.Increment(ref _calls) == 1)
                await competitor();
            return await inner.ExecuteAsync(aggregate, operation, context, ct);
        }
    }

    /// <summary>Commits the first attempt but reports it as unknown, as a timeout after append would.</summary>
    sealed class CommitThenFailOnceExecutor(IAggregateExecutor inner) : IAggregateExecutor
    {
        int _calls;

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);

        public async ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate
        {
            var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
            if (Interlocked.Increment(ref _calls) == 1)
                throw new EventStreamConcurrencyException("Append outcome unknown.", new TimeoutException());
            return result;
        }
    }

    sealed class AlwaysConflictExecutor : IAggregateExecutor
    {
        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate =>
            throw new EventStreamConcurrencyException("Lost the append race.", new InvalidOperationException());

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context, CancellationToken ct)
            where TAggregate : Aggregate =>
            throw new EventStreamConcurrencyException("Lost the append race.", new InvalidOperationException());
    }

    /// <summary>Writes normally but fails every verified read, as corrupt or missing content does.</summary>
    sealed class CorruptReadStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) =>
            throw new ImmutableContentIntegrityException("The stored content does not match its reference.");
    }
}
