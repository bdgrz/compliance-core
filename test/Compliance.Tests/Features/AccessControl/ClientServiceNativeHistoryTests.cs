using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ClientServiceNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateMiddleServicePrefixGivenCapturedClientAuthority(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        for (var index = 0; index < 3; index++)
            Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), index,
                IndependenceHost.Service()))).IsSuccess);
        var owner = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var facts = await FactsAsync(host, owner.Stream);
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var original = (NonattestServiceRecorded)facts[1];
            var changed = fault == "payload" ? original with { ServiceContent = original.ServiceContent with { ContentId = Uuid.CreateVersion4() } } : original with { };
            changed.AttachMetadata(fault == "event_id" ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
            facts[1] = changed;
        }

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await ClientServiceNativeHistory.ReadAsync(new Records(owner.Stream, facts), owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3UL, owner.Epoch);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("physical")]
    [InlineData("event_id")]
    [InlineData("metadata")]
    [InlineData("payload")]
    [InlineData("missing")]
    public async Task ShouldRejectGlobalWitnessGivenDifferentTailTuplePayloadOrMetadata(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        var owner = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var events = host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var resolver = new DifferentWitness(new EventStoreSourceFactResolver(events), fault);

        // Act
        var failure = await Record.ExceptionAsync(() => IndependenceSourceCut.CaptureAsync(events, resolver, owner,
            TimeProvider.System, CancellationToken.None));

        // Assert
        Assert.Equal(1, resolver.Calls);
        if (fault == "missing")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(1UL, owner.Epoch);
    }

    [Fact]
    public async Task ShouldSealEmptyGlobalCutWithoutResolverCallGivenEmptyCapturedOwner()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var owner = new ClientServiceIndependence(host.Tenant);
        var events = host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var resolver = new DifferentWitness(new EventStoreSourceFactResolver(events), "missing");

        // Act
        var cut = await IndependenceSourceCut.CaptureAsync(events, resolver, owner, TimeProvider.System, CancellationToken.None);

        // Assert
        Assert.Equal(0UL, cut.GlobalWatermarkExclusive);
        Assert.Equal($"{owner.Stream.Realm}/{owner.Stream.Area}/{owner.Stream.Resource}", cut.AnchorSelector);
        Assert.Equal(0, resolver.Calls);
    }

    [Fact]
    public async Task ShouldFailClosedWithoutResolverCallGivenCapturedTailIsAuditRecord()
    {
        // Arrange
        await using var host = new IndependenceHost();
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 0,
            IndependenceHost.Service()))).IsSuccess);
        var captured = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        var original = captured.LatestService!;
        var audit = original with { };
        audit.AttachMetadata(original.Metadata with { EventId = Uuid.CreateVersion4(), IsAudit = true });
        var events = host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var stream = new ClientServiceIndependence(host.Tenant).Stream;
        await host.Scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(stream, 1, [audit], CancellationToken.None);
        // Exercise a retained legacy physical cut directly: SDK 0.9.1 keeps current audit sessions
        // on separate streams and correctly refuses to hydrate an audit as a state change.
        typeof(Aggregate).GetProperty(nameof(Aggregate.CommittedStreamPosition))!.SetValue(captured, 2UL);
        var resolver = new DifferentWitness(new EventStoreSourceFactResolver(events), "missing");

        // Act
        var failure = await Record.ExceptionAsync(() => IndependenceSourceCut.CaptureAsync(events, resolver, captured,
            TimeProvider.System, CancellationToken.None));

        // Assert
        Assert.Equal(2UL, captured.CommittedStreamPosition);
        Assert.IsType<ImmutableContentUnavailableException>(failure);
        Assert.Equal(0, resolver.Calls);
    }

    static async Task<DomainEvent[]> FactsAsync(IndependenceHost host, EventStreamAddress stream)
    {
        var found = new List<DomainEvent>();
        await foreach (var record in host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(stream, 0))
            found.Add(record.Event);
        return found.ToArray();
    }

    sealed class DifferentWitness(ISourceFactResolver inner, string fault) : ISourceFactResolver
    {
        public int Calls { get; private set; }
        public async Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong position, CancellationToken ct = default)
        {
            Calls++;
            if (fault == "missing")
                throw new SourceFactUnavailableException("The exact broker witness is unavailable.");
            var resolved = await inner.ResolveAsync(stream, position, ct);
            var locator = resolved.Locator;
            var body = resolved.Event;
            if (fault == "scope")
                locator = locator with { Source = new ClientServiceIndependence(Uuid.CreateVersion4()).Stream };
            if (fault == "physical")
                locator = locator with { ResourcePosition = position + 1 };
            if (fault == "event_id")
                locator = locator with { EventId = Uuid.CreateVersion4() };
            if (fault is "metadata" or "payload")
            {
                var original = (NonattestServiceRecorded)body;
                var changed = fault == "payload" ? original with { ServiceContent = original.ServiceContent with { Sha256 = new string('f', 64) } } : original with { };
                changed.AttachMetadata(fault == "metadata" ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
                body = changed;
            }
            return new ResolvedSourceFact(body, locator);
        }
        public Task<VerifiedSourceProof<TEvent>> VerifyAsync<TEvent>(SourceFactProof<TEvent> proof,
            CancellationToken ct = default) where TEvent : DomainEvent => throw new NotSupportedException();
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset, CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
