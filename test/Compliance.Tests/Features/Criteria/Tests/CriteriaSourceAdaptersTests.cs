using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaSourceAdaptersTests
{
    static readonly EventStreamAddress Target = new("bdgrz", "criteria-test", "target");
    static readonly EventStreamAddress Other = new("bdgrz", "criteria-test", "other");

    [Fact]
    public async Task ShouldCoverTargetPrefixGivenTheAreaCutIsObserved()
    {
        // Arrange
        var streams = Pages(Record(Target, 0, 0), Record(Other, 0, 1), Record(Target, 1, 2));
        var coverage = new FitzCriteriaSourceCutCoverage(streams);

        // Act
        // Assert
        Assert.True(await coverage.CoversAsync(Target, 1, 2));
        Assert.False(await coverage.CoversAsync(Target, 0, 2));
        Assert.False(await coverage.CoversAsync(Target, 2, 3));
    }

    [Fact]
    public async Task ShouldNotCoverCutGivenAnAreaGap()
    {
        // Arrange
        var coverage = new FitzCriteriaSourceCutCoverage(Pages(Record(Target, 0, 0), Record(Other, 0, 2)));

        // Act
        // Assert
        Assert.False(await coverage.CoversAsync(Target, 1, 2));
    }

    [Fact]
    public async Task ShouldVerifyCommittedOrderGivenEventsShareARealm()
    {
        // Arrange
        var first = Fact(Target, 0, 1, 4);
        var second = Fact(Other, 0, 2, 5);
        var source = new ExactSource(first, second);
        var order = new FitzCriteriaParticipantCausalOrder(source);

        // Act
        // Assert
        Assert.True(await order.PrecedesAsync(first, second));
        Assert.False(await order.PrecedesAsync(second, first));
        Assert.False(await order.PrecedesAsync(first, second with { RealmOffset = 6 }));
        Assert.False(await order.PrecedesAsync(first, second with { RealmOffset = null }));
    }

    [Fact]
    public async Task ShouldCompareCommittedHeadGivenNoReservationIsRequired()
    {
        // Arrange
        var fact = Fact(Target, 0, 1, 1);
        var reader = new CommittedReader(fact);
        var heads = new PortiaCriteriaSourceHead(reader);

        // Act
        // Assert
        Assert.True(await heads.IsCurrentAsync(Target, fact.Event.Metadata.AggregateId, 1));
        Assert.False(await heads.IsCurrentAsync(Target, fact.Event.Metadata.AggregateId, 0));
        Assert.False(await heads.IsCurrentAsync(Target, Uuid.CreateVersion4(), 1));
    }

    static ResolverStreamClient Pages(params StreamRecord[] records) => new((_, start) =>
    {
        var items = records.Select(record => new StreamReadItem(record.Route, StreamReadItemKind.Event, record)).ToArray();
        var next = records.Length == 0 ? start : checked(records[^1].AreaOffset!.Value + 1);
        return [new StreamReadBatch(items, start, next, true)];
    });

    static StreamRecord Record(EventStreamAddress stream, ulong resource, ulong area) =>
        new(stream.ToString(), resource, area, area, null, new byte[] { 1 }, null, 0);

    static CriteriaOrderedSourceEvent Fact(EventStreamAddress stream, ulong resource, ulong area, ulong realm)
    {
        var fact = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Example", "example");
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), resource + 1, DateTimeOffset.UtcNow));
        return new CriteriaOrderedSourceEvent(stream, resource, area, fact) { RealmOffset = realm };
    }

    sealed class ExactSource(params CriteriaOrderedSourceEvent[] records) : ICriteriaExactSourceReader
    {
        public ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
            Uuid eventId, CancellationToken ct = default) => ValueTask.FromResult(records.FirstOrDefault(record =>
                record.Stream == stream && record.Event.Metadata.AggregateId == aggregateId && record.Event.Metadata.EventId == eventId));

        public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            foreach (var record in records.Where(record => record.Stream == stream && record.Event.Metadata.AggregateId == aggregateId))
            {
                ct.ThrowIfCancellationRequested();
                yield return record;
            }
        }
    }

    sealed class CommittedReader(params CriteriaOrderedSourceEvent[] records) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            foreach (var record in records.Where(record => record.Stream == stream && record.ResourceOffset >= fromOffset))
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, record.Event, record.ResourceOffset, EventCursor.Start);
            }
        }

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
