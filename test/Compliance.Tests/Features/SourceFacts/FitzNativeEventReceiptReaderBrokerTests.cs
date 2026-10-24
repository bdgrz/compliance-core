using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class FitzNativeEventReceiptReaderBrokerTests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldExposeActualBrokerCommitMillisecondsGivenOldOccurrenceAndPreparationCut()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var http = factory.CreateClient();
        var stream = new EventStreamAddress("receipt-" + Guid.NewGuid().ToString("N"), "tenants", "acme");
        var events = factory.Services.GetRequiredService<IEventStore>();
        var serializer = factory.Services.GetRequiredService<IDomainEventSerializer>();
        var before = DateTimeOffset.UtcNow;
        var cutoff = before.AddMinutes(-1);
        var value = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Acme", "acme");
        value.AttachMetadata(new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, cutoff.AddDays(-1)));
        await events.AppendAsync(stream, 0, [value]);
        var after = DateTimeOffset.UtcNow;
        var records = new List<DomainEventRecord>();
        await foreach (var record in events.ReadAsync(stream, 0, CancellationToken.None))
            records.Add(record);
        var source = Assert.Single(records);
        await using var fitz = new Client(new ClientConfig(new Uri(broker.WebSocketEndpoint, UriKind.Absolute), Timeout: TimeSpan.FromSeconds(10)));
        await fitz.ConnectWhenReadyAsync(new ConnectWhenReadyOptions(TimeSpan.FromSeconds(15)));
        var reader = new FitzNativeEventReceiptReader(fitz.Stream, serializer);

        // Act
        var recordedAt = await reader.RecordedAtAsync(source);
        var raw = await fitz.Stream.ReadPageAsync(stream.ToString(), 0, 1);

        // Assert
        var committed = Assert.Single(raw.Items).Record!;
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(checked((long)committed.Timestamp)), recordedAt);
        Assert.True(recordedAt > cutoff);
        Assert.Equal(value.Metadata.OccurredOn, source.Event.Metadata.OccurredOn);
        Assert.InRange(recordedAt, before.AddSeconds(-5), after.AddSeconds(5));
        Assert.Equal(0, recordedAt.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.Equal(source.Stream.ToString(), committed.Route);
        Assert.Equal(source.ResourceOffset, committed.Offset);
    }
}
