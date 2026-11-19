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
    public async Task ShouldCoverTargetPrefixOnlyAfterObservingAreaCut()
    {
        // Arrange
        var streams = Pages(Record(Target, 0, 0), Record(Other, 0, 1), Record(Target, 1, 2));
        var coverage = new FitzCriteriaSourceCutCoverage(streams);

        // Act and Assert
        Assert.True(await coverage.CoversAsync(Target, 1, 2));
        Assert.False(await coverage.CoversAsync(Target, 0, 2));
        Assert.False(await coverage.CoversAsync(Target, 2, 3));
    }

    [Fact]
    public async Task ShouldNotCoverCutGivenAnAreaGap()
    {
        // Arrange
        var coverage = new FitzCriteriaSourceCutCoverage(Pages(Record(Target, 0, 0), Record(Other, 0, 2)));

        // Act and Assert
        Assert.False(await coverage.CoversAsync(Target, 1, 2));
    }

    [Fact]
    public async Task ShouldVerifyExactSameRealmCommittedOrder()
    {
        // Arrange
        var first = Fact(Target, 0, 1, 4);
        var second = Fact(Other, 0, 2, 5);
        var source = new ExactSource(first, second);
        var order = new FitzCriteriaParticipantCausalOrder(source);

        // Act and Assert
        Assert.True(await order.PrecedesAsync(first, second));
        Assert.False(await order.PrecedesAsync(second, first));
        Assert.False(await order.PrecedesAsync(first, second with { RealmOffset = 6 }));
        Assert.False(await order.PrecedesAsync(first, second with { RealmOffset = null }));
    }

    [Fact]
    public async Task ShouldCompareExactCommittedHeadWithoutReservingIt()
    {
        // Arrange
        var fact = Fact(Target, 0, 1, 1);
        var reader = new CommittedReader(fact);
        var heads = new PortiaCriteriaSourceHead(reader);

        // Act and Assert
        Assert.True(await heads.IsCurrentAsync(Target, fact.Event.Metadata.AggregateId, 1));
        Assert.False(await heads.IsCurrentAsync(Target, fact.Event.Metadata.AggregateId, 0));
        Assert.False(await heads.IsCurrentAsync(Target, Uuid.CreateVersion4(), 1));
    }

    static ResolverStreamClient Pages(params StreamRecord[] records) => new((_, start, _, _) =>
    {
        if (start >= (ulong)records.Length)
            return new StreamReadPage([], new StreamReadCursor(0, null, null, null, null, null, false));
        var record = records[checked((int)start)];
        return new StreamReadPage([new StreamReadItem(record.Route, StreamReadItemKind.Event, record, 0, 0, 0, null)],
            new StreamReadCursor(record.Offset, record.AreaOffset, record.RealmOffset, null, null, null,
                start + 1 < (ulong)records.Length));
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
