using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

public sealed class RecordedSourceReceiptCutTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Owner = Uuid.CreateVersion4();
    static readonly EventStreamAddress Stream = new(Tenant.ToString(), "tenants", Owner.ToString());
    static readonly DateTimeOffset Cut = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldExcludePreparedFactGivenCommitReceiptAfterInclusiveCut()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddHours(-2), Cut.AddHours(-1), Cut.AddMilliseconds(-1), Cut.AddMilliseconds(1));

        // Act
        var selected = await fixture.Cut.ReadAsync(Stream);

        // Assert
        Assert.True(selected.Includes(fixture.Facts[0]));
        Assert.False(selected.Includes(fixture.Facts[1]));
        Assert.Equal(Cut.AddMilliseconds(1), selected.RecordedAt(fixture.Facts[1]));
    }

    [Fact]
    public async Task ShouldAcceptCausalPrefixGivenOccurrenceTimestampsDescend()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddMinutes(-1), Cut.AddDays(-1), Cut.AddMilliseconds(-2), Cut.AddMilliseconds(-1));

        // Act
        var selected = await fixture.Cut.ReadAsync(Stream);

        // Assert
        Assert.Equal(2, selected.Included.Count());
        Assert.True(selected.Includes(fixture.Facts[0]));
        Assert.True(selected.Includes(fixture.Facts[1]));
    }

    [Fact]
    public async Task ShouldFailClosedGivenReceiptCutWouldSelectNonPrefix()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddMilliseconds(1), Cut.AddMilliseconds(-1));

        // Act
        var failure = await Record.ExceptionAsync(async () => await fixture.Cut.ReadAsync(Stream));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldIncludeAllTiedReceiptsGivenSameBrokerMillisecondAsCut()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddTicks(100), Cut.AddTicks(9000));

        // Act
        var selected = await fixture.Cut.ReadAsync(Stream);

        // Assert
        Assert.Equal(2, selected.Included.Count());
        Assert.All(selected.Records, record => Assert.Equal(Cut, selected.RecordedAt(record.Event)));
    }

    [Fact]
    public async Task ShouldReuseOneCapturedReceiptStreamGivenConcurrentFamilyReads()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddMilliseconds(-2), Cut.AddMilliseconds(-1));

        // Act
        var reads = await Task.WhenAll(fixture.Cut.ReadAsync(Stream), fixture.Cut.ReadAsync(Stream), fixture.Cut.ReadAsync(Stream));

        // Assert
        Assert.Same(reads[0], reads[1]);
        Assert.Same(reads[0], reads[2]);
        Assert.Equal(2, fixture.Receipts.Reads);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("version")]
    [InlineData("metadata")]
    public async Task ShouldRefuseSelectedSourceGivenTupleOrBytesDiffer(string changed)
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddMilliseconds(-2), Cut.AddMilliseconds(-1));
        var selected = await fixture.Cut.ReadAsync(Stream);
        var original = (TenantRegistered)fixture.Facts[0];
        var altered = original with { Name = changed == "payload" ? "Altered" : original.Name };
        altered.AttachMetadata(original.Metadata with
        {
            AggregateVersion = changed == "version" ? 9UL : original.Metadata.AggregateVersion,
            OccurredOn = changed == "metadata" ? original.Metadata.OccurredOn.AddSeconds(1) : original.Metadata.OccurredOn
        });

        // Act
        var failure = Record.Exception(() => selected.Includes(altered));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenExactReceiptAbsent()
    {
        // Arrange
        var source = new InMemoryEventStore();
        var fact = Fact(1, Cut.AddDays(-1));
        await source.AppendAsync(Stream, 0, [fact]);
        var cut = new RecordedSourceReceiptCutFactory(source, new ScriptedNativeEventReceipts()).Create(Tenant, Cut);

        // Act
        var failure = await Record.ExceptionAsync(async () => await cut.ReadAsync(Stream));

        // Assert
        Assert.IsType<ImmutableContentUnavailableException>(failure);
    }

    [Fact]
    public async Task ShouldRejectForeignRealmGivenScopedReceiptSelection()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddMilliseconds(-2), Cut.AddMilliseconds(-1));

        // Act
        var failure = await Record.ExceptionAsync(async () => await fixture.Cut.ReadAsync(new(Uuid.CreateVersion4().ToString(), Stream.Area, Stream.Resource)));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(0, fixture.Receipts.Reads);
    }

    [Fact]
    public async Task ShouldAvoidReceiptAuthorityGivenOrdinaryTypeISourceContext()
    {
        // Arrange
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, []);
        var unknown = Fact(1, Cut.AddYears(1));

        // Act
        var included = await context.IncludesAsync(Stream, unknown, CancellationToken.None);

        // Assert
        Assert.True(included);
        Assert.Null(context.ReceiptCut);
    }

    static async Task<(RecordedSourceReceiptCut Cut, ScriptedNativeEventReceipts Receipts, IReadOnlyList<DomainEvent> Facts)> FixtureAsync(
        DateTimeOffset firstOccurred, DateTimeOffset secondOccurred, DateTimeOffset firstCommitted, DateTimeOffset secondCommitted)
    {
        var events = new InMemoryEventStore();
        DomainEvent[] facts = [Fact(1, firstOccurred), Fact(2, secondOccurred)];
        await events.AppendAsync(Stream, 0, facts);
        var receipts = new ScriptedNativeEventReceipts();
        await foreach (var record in events.ReadAsync(Stream, 0, CancellationToken.None))
            receipts.Record(record, record.ResourceOffset == 0 ? firstCommitted : secondCommitted);
        return (new RecordedSourceReceiptCutFactory(events, receipts).Create(Tenant, Cut), receipts, facts);
    }
    static TenantRegistered Fact(ulong version, DateTimeOffset occurred)
    {
        var fact = new TenantRegistered(Tenant, Uuid.CreateVersion4(), "Original", "original");
        fact.AttachMetadata(new(Uuid.CreateVersion4(), Owner, version, occurred));
        return fact;
    }
}
