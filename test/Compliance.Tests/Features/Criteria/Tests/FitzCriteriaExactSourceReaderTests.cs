using System.Text;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class FitzCriteriaExactSourceReaderTests
{
    static readonly EventStreamAddress Stream = new("bdgrz", "criteria-test", "entry");

    [Fact]
    public async Task ShouldPreserveRealmOffsetsGivenPresentAndMissingObservationsAcrossPages()
    {
        // Arrange
        var fixture = new Fixture(0, null, 7);

        // Act
        var records = await fixture.ReadAsync();

        // Assert
        Assert.Equal(3, records.Count);
        Assert.Equal(0UL, records[0].RealmOffset);
        Assert.Null(records[1].RealmOffset);
        Assert.Equal(7UL, records[2].RealmOffset);
    }

    [Theory]
    [InlineData(8UL, 8UL)]
    [InlineData(8UL, 7UL)]
    public async Task ShouldRejectNonIncreasingRealmOffsetsGivenAnObservationIsMissing(ulong first, ulong last)
    {
        // Arrange
        var fixture = new Fixture(first, null, last);

        // Act
        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.ReadAsync());
    }

    [Fact]
    public async Task ShouldRetainRealmOffsetGivenAnExactEventLookup()
    {
        // Arrange
        var fixture = new Fixture(null, 23);

        // Act
        var found = await fixture.Reader.FindAsync(Stream, fixture.AggregateId, fixture.Events[1].Metadata.EventId);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(23UL, found.RealmOffset);
    }

    sealed class Fixture
    {
        public Uuid AggregateId { get; } = Uuid.CreateVersion4();
        public List<DomainEvent> Events { get; } = [];
        public FitzCriteriaExactSourceReader Reader { get; }

        public Fixture(params ulong?[] realmOffsets)
        {
            var records = new List<StreamRecord>();
            for (var index = 0; index < realmOffsets.Length; index++)
            {
                var fact = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Acme", "acme");
                fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), AggregateId, (ulong)index + 1,
                    DateTimeOffset.UtcNow));
                Events.Add(fact);
                records.Add(new StreamRecord(Stream.ToString(), (ulong)index, (ulong)index + 10, realmOffsets[index], null,
                    new byte[] { checked((byte)index) }, Encoding.UTF8.GetBytes(Stream.ToString()), 0));
            }
            var streams = new ResolverStreamClient((_, start) =>
            {
                var items = records.Skip(checked((int)start))
                    .Select(record => new StreamReadItem(record.Route, StreamReadItemKind.Event, record))
                    .ToArray();
                return [new StreamReadBatch(items, start, checked((ulong)records.Count), true)];
            });
            Reader = new FitzCriteriaExactSourceReader(streams, new Serializer(Events));
        }

        public async Task<List<CriteriaOrderedSourceEvent>> ReadAsync()
        {
            var records = new List<CriteriaOrderedSourceEvent>();
            await foreach (var record in Reader.ReadAsync(Stream, AggregateId))
                records.Add(record);
            return records;
        }
    }

    sealed class Serializer(IReadOnlyList<DomainEvent> events) : IDomainEventSerializer
    {
        public ReadOnlyMemory<byte> Serialize(DomainEvent ev) => throw new NotSupportedException();
        public DomainEvent Deserialize(ReadOnlyMemory<byte> data) => events[data.Span[0]];
    }
}
