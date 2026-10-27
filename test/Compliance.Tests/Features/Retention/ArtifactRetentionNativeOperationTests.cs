using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Handlers;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Queries;
using Bdgrz.Compliance.Features.Retention.Readers;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class ArtifactRetentionNativeOperationTests
{
    [Fact]
    public async Task ShouldReturnOriginalResultGivenPlacementRetryAfterRelease()
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = await fixture.EvidenceAsync();
        var request = fixture.Place(source);
        var handler = new PlaceArtifactLegalHoldHandler(fixture.Mutation());
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var release = fixture.Release(source, request.HoldId);
        Assert.True((await new ReleaseArtifactLegalHoldHandler(fixture.Mutation())
            .HandleAsync(fixture.Context(release), CancellationToken.None)).IsSuccess);
        var noSource = new NoSourceReader(fixture.Reader);
        var retry = new PlaceArtifactLegalHoldHandler(fixture.Mutation(noSource));

        // Act
        var result = await retry.HandleAsync(fixture.Context(request), CancellationToken.None);
        var held = await fixture.Reader.HydrateAsync(new ArtifactLegalHold(fixture.Tenant, request.HoldId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, held.Revision);
        Assert.Equal(2UL, held.CommittedStreamPosition);
        Assert.NotNull(held.View!.ReleasedAt);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultWithoutReselectingSourceGivenBasisRetry()
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = await fixture.EvidenceAsync();
        var request = fixture.Basis(source);
        Assert.True((await new RecordArtifactRetentionBasisHandler(fixture.Mutation())
            .HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var noSource = new NoSourceReader(fixture.Reader);

        // Act
        var result = await new RecordArtifactRetentionBasisHandler(fixture.Mutation(noSource))
            .HandleAsync(fixture.Context(request), CancellationToken.None);
        var basis = await fixture.Reader.HydrateAsync(new ArtifactRetentionBasis(source));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1UL, basis.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("place")]
    [InlineData("release")]
    public async Task ShouldConflictGivenSameIdentityWithChangedActorIntentOrOriginalRevision(string operation)
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = await fixture.EvidenceAsync();
        var basis = fixture.Basis(source);
        var place = fixture.Place(source);
        var release = fixture.Release(source, place.HoldId);
        if (operation != "basis")
            Assert.True((await new PlaceArtifactLegalHoldHandler(fixture.Mutation())
                .HandleAsync(fixture.Context(place), CancellationToken.None)).IsSuccess);
        if (operation == "basis")
            Assert.True((await new RecordArtifactRetentionBasisHandler(fixture.Mutation())
                .HandleAsync(fixture.Context(basis), CancellationToken.None)).IsSuccess);
        if (operation == "release")
            Assert.True((await new ReleaseArtifactLegalHoldHandler(fixture.Mutation())
                .HandleAsync(fixture.Context(release), CancellationToken.None)).IsSuccess);

        // Act
        var results = new List<Result>();
        foreach (var change in new[] { "actor", "reason", "revision", "content", "source" })
        {
            var actor = change == "actor" ? Fixture.Actor(Uuid.CreateVersion4()) : fixture.User;
            results.Add(operation switch
            {
                "basis" => await new RecordArtifactRetentionBasisHandler(fixture.Mutation()).HandleAsync(
                    fixture.Context(basis with
                    {
                        Reason = change == "reason" ? "Changed" : basis.Reason,
                        ExpectedRevision = change == "revision" ? 1 : basis.ExpectedRevision,
                        // A different content digest determines another first owner for a basis; that cross-owner
                        // reuse remains #1042's prerequisite. Exercise authored period/source fields on this owner.
                        PeriodStart = change == "content" ? new DateOnly(2020, 1, 1) : basis.PeriodStart,
                        PeriodEnd = change == "source" ? new DateOnly(2020, 12, 31) : basis.PeriodEnd,
                    }, actor), CancellationToken.None),
                "place" => await new PlaceArtifactLegalHoldHandler(fixture.Mutation()).HandleAsync(
                    fixture.Context(place with
                    {
                        Reason = change == "reason" ? "Changed" : place.Reason,
                        ExpectedRevision = change == "revision" ? 1 : place.ExpectedRevision,
                        ExpectedContentSha256 = change == "content" ? new string('b', 64) : place.ExpectedContentSha256,
                        SourceId = change == "source" ? Uuid.CreateVersion4() : place.SourceId,
                    }, actor), CancellationToken.None),
                _ => await new ReleaseArtifactLegalHoldHandler(fixture.Mutation()).HandleAsync(
                    fixture.Context(release with
                    {
                        Reason = change == "reason" ? "Changed" : release.Reason,
                        ExpectedRevision = change == "revision" ? 2 : release.ExpectedRevision,
                        ExpectedContentSha256 = change == "content" ? new string('b', 64) : release.ExpectedContentSha256,
                        SourceId = change == "source" ? Uuid.CreateVersion4() : release.SourceId,
                    }, actor), CancellationToken.None),
            });
        }

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind));
        if (operation == "basis")
            Assert.Equal(1UL, (await fixture.Reader.HydrateAsync(new ArtifactRetentionBasis(source))).CommittedStreamPosition);
        else
            Assert.Equal(operation == "release" ? 2UL : 1UL,
                (await fixture.Reader.HydrateAsync(new ArtifactLegalHold(fixture.Tenant, place.HoldId))).CommittedStreamPosition);
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("place")]
    [InlineData("release")]
    public async Task ShouldRejectWithoutAppendGivenMissingCommandIdentity(string operation)
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = await fixture.EvidenceAsync();
        var holdId = Uuid.CreateVersion4();

        // Act
        var result = operation switch
        {
            "basis" => await new RecordArtifactRetentionBasisHandler(fixture.Mutation()).HandleAsync(
                fixture.Context(fixture.Basis(source) with { CommandId = Uuid.Empty }), CancellationToken.None),
            "place" => await new PlaceArtifactLegalHoldHandler(fixture.Mutation()).HandleAsync(
                fixture.Context(fixture.Place(source) with { HoldId = Uuid.Empty }), CancellationToken.None),
            _ => await new ReleaseArtifactLegalHoldHandler(fixture.Mutation()).HandleAsync(
                fixture.Context(fixture.Release(source, holdId) with { CommandId = Uuid.Empty }), CancellationToken.None),
        };

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0UL, (await fixture.Reader.HydrateAsync(new ArtifactRetentionBasis(source))).CommittedStreamPosition);
        Assert.Equal(0UL, (await fixture.Reader.HydrateAsync(new ArtifactLegalHold(fixture.Tenant, holdId))).CommittedStreamPosition);
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("place")]
    [InlineData("release")]
    public async Task ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome(string operation)
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = await fixture.EvidenceAsync();
        var place = fixture.Place(source);
        if (operation == "release")
            Assert.True((await new PlaceArtifactLegalHoldHandler(fixture.Mutation())
                .HandleAsync(fixture.Context(place), CancellationToken.None)).IsSuccess);
        var mutation = fixture.Mutation(executor: new CommitThenFailOnceExecutor(fixture.Executor));

        // Act
        var result = operation switch
        {
            "basis" => await new RecordArtifactRetentionBasisHandler(mutation).HandleAsync(
                fixture.Context(fixture.Basis(source)), CancellationToken.None),
            "place" => await new PlaceArtifactLegalHoldHandler(mutation).HandleAsync(fixture.Context(place), CancellationToken.None),
            _ => await new ReleaseArtifactLegalHoldHandler(mutation).HandleAsync(
                fixture.Context(fixture.Release(source, place.HoldId)), CancellationToken.None),
        };

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        if (operation == "basis")
            Assert.Equal(1UL, (await fixture.Reader.HydrateAsync(new ArtifactRetentionBasis(source))).CommittedStreamPosition);
        else
            Assert.Equal(operation == "release" ? 2UL : 1UL,
                (await fixture.Reader.HydrateAsync(new ArtifactLegalHold(fixture.Tenant, place.HoldId))).CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldCommitOneCreationGivenEqualConcurrentHoldIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = await fixture.EvidenceAsync();
        var request = fixture.Place(source);
        var handler = new PlaceArtifactLegalHoldHandler(fixture.Mutation());

        // Act
        var results = await Task.WhenAll(handler.HandleAsync(fixture.Context(request), CancellationToken.None).AsTask(),
            handler.HandleAsync(fixture.Context(request), CancellationToken.None).AsTask());
        var hold = await fixture.Reader.HydrateAsync(new ArtifactLegalHold(fixture.Tenant, request.HoldId));

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(1UL, hold.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldKeepEveryIndependentHoldGivenMoreThanSixtyFourPlacements()
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = await fixture.EvidenceAsync();
        var handler = new PlaceArtifactLegalHoldHandler(fixture.Mutation());
        for (var index = 0; index < 70; index++)
            Assert.True((await handler.HandleAsync(fixture.Context(fixture.Place(source)), CancellationToken.None)).IsSuccess);
        var captured = await ArtifactRetentionSourceReader.LoadAsync(fixture.Reader, fixture.Tenant,
            source.SourceKind, source.SourceId, CancellationToken.None);

        // Act
        var snapshot = await new ArtifactRetentionReader(fixture.Reader, fixture.Events).ReadAsync(captured.Value);

        // Assert
        Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
        Assert.Equal(70, snapshot.Value.Holds.Count);
        Assert.Equal(70, snapshot.Value.Assess(new DateOnly(2050, 1, 1)).ActiveLegalHoldCount);
        Assert.False(snapshot.Value.Assess(new DateOnly(2050, 1, 1)).DispositionAllowed);
    }

    [Fact]
    public async Task ShouldFailClosedGivenUnmigratedCombinedRetentionHistory()
    {
        // Arrange
        await using var fixture = new Fixture();
        var source = await fixture.EvidenceAsync();
        await fixture.SeedLegacyAsync(source);

        // Act
        var read = await new ArtifactRetentionRead(fixture.Reader, fixture.Events, TimeProvider.System)
            .GetAsync(fixture.Tenant, source.SourceKind, source.SourceId, null, CancellationToken.None);
        var write = await new PlaceArtifactLegalHoldHandler(fixture.Mutation())
            .HandleAsync(fixture.Context(fixture.Place(source)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, read.Error?.Kind);
        Assert.Contains("migration", read.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, write.Error?.Kind);
    }

    sealed class LegacyRetentionSeed : Aggregate
    {
        public LegacyRetentionSeed(ArtifactRetentionSource source)
            : base(Uuid.CreateVersion5(source.TenantId, $"artifact_retention:{source.SourceKind}:{source.SourceId}"),
                new EventStreamAddress(source.TenantId.ToString(), "artifact_retention",
                    Uuid.CreateVersion5(source.TenantId, $"artifact_retention:{source.SourceKind}:{source.SourceId}").ToString()))
        {
            On<ArtifactRetentionBound>(_ => { });
            var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Historical administrator");
            var id = Uuid.CreateVersion4();
            var at = DateTimeOffset.UtcNow;
            var operation = new OperationProvenance(id, id, null, "RecordArtifactRetentionBasis", actor,
                Stream, "initial", 0, new string('a', 64), at, Stream, Stream);
            RaiseEvent(new ArtifactRetentionBound(source, 1, 1, actor, at, operation));
        }
    }

    sealed class NoSourceReader(IAggregateReader inner) : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate =>
            aggregate is EvidenceArtifact ? throw new InvalidOperationException("An accepted retry must not reselect source state.")
                : inner.HydrateAsync(aggregate, ct);
    }

    sealed class CommitThenFailOnceExecutor(IAggregateExecutor inner) : IAggregateExecutor
    {
        int _calls;
        public ValueTask<Result> ExecuteAsync<T>(T aggregate, Func<T, AggregateOutcome> operation,
            IExecutionContext context, CancellationToken ct) where T : Aggregate => inner.ExecuteAsync(aggregate, operation, context, ct);
        public async ValueTask<Result<TResult>> ExecuteAsync<T, TResult>(T aggregate,
            Func<T, AggregateOutcome<TResult>> operation, IExecutionContext context, CancellationToken ct) where T : Aggregate
        {
            var result = await inner.ExecuteAsync(aggregate, operation, context, ct);
            if (Interlocked.Increment(ref _calls) == 1)
                throw new EventStreamConcurrencyException("Native append outcome unknown.", new TimeoutException());
            return result;
        }
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IAggregateWriter _writer;
        readonly IImmutableContentStore _content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal User { get; } = Actor(Uuid.CreateVersion4());
        public IAggregateReader Reader { get; }
        public IAggregateExecutor Executor { get; }
        public IDomainEventReader Events { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddPortia();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Events = events;
            _writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }
        public ArtifactRetentionMutation Mutation(IAggregateReader? reader = null, IAggregateExecutor? executor = null) =>
            new(new NativeOperationExecutor(reader ?? Reader, executor ?? Executor, _content), reader ?? Reader, _content, TimeProvider.System, Events);
        public static ClaimsPrincipal Actor(Uuid id) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", id.ToString())], "BdgrzSession"));
        public Cntryl.Portia.RequestContext<T> Context<T>(T request, ClaimsPrincipal? actor = null) where T : IRequestBase =>
            new(request, new RequestDispatchContext(actor ?? User,
                new HttpInvocation("PUT", "/synthetic/retention", null, "retention-native-test")));
        public RecordArtifactRetentionBasis Basis(ArtifactRetentionSource source) =>
            new(Uuid.CreateVersion4(), Tenant, source.SourceKind, source.SourceId, source.ContentSha256, 0, "Native period verified");
        public PlaceArtifactLegalHold Place(ArtifactRetentionSource source) =>
            new(Tenant, source.SourceKind, source.SourceId, source.ContentSha256, Uuid.CreateVersion4(), 0, "Legal matter");
        public ReleaseArtifactLegalHold Release(ArtifactRetentionSource source, Uuid hold) =>
            new(Uuid.CreateVersion4(), Tenant, source.SourceKind, source.SourceId, source.ContentSha256, hold, 1, "Matter closed");
        public Task SeedLegacyAsync(ArtifactRetentionSource source) =>
            _writer.SaveAsync(new LegacyRetentionSeed(source), Context(new GetArtifactRetention(Tenant, source.SourceKind, source.SourceId))).AsTask();
        public async Task<ArtifactRetentionSource> EvidenceAsync()
        {
            var id = Uuid.CreateVersion4();
            var sha = new string('a', 64);
            var artifact = new EvidenceArtifact(Tenant, id);
            Assert.True(artifact.Register(new("Evidence", null, "export", "manual", DateTimeOffset.UtcNow,
                    new(2019, 1, 1), new(2019, 12, 31), "confidential"), sha, 100,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Collector"), DateTimeOffset.UtcNow).IsSuccess);
            await _writer.SaveAsync(artifact, Context(new GetArtifactRetention(Tenant, "evidence_artifact", id)));
            return new(Tenant, "evidence_artifact", id, sha);
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
