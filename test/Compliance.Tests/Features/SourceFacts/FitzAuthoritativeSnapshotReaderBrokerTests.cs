using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class FitzAuthoritativeSnapshotReaderBrokerTests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldReadOnlyPublicationsInsideCutGivenGlobalSelectorOnBroker()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var client = factory.CreateClient();
        var realm = $"k5-{Guid.NewGuid():N}";
        var stream = new EventStreamAddress(realm, "tenants", "acme");
        var events = factory.Services.GetRequiredService<IEventStore>();
        await Append(events, stream, 0);
        await Append(events, stream, 1);
        await using var fitz = new Client(new ClientConfig(new Uri(broker.WebSocketEndpoint, UriKind.Absolute),
            Timeout: TimeSpan.FromSeconds(10)));
        await fitz.ConnectWhenReadyAsync(new ConnectWhenReadyOptions(TimeSpan.FromSeconds(15)));
        var reader = new FitzAuthoritativeSnapshotReader(fitz.Stream,
            factory.Services.GetRequiredService<IDomainEventSerializer>());
        var capture = await reader.CaptureAsync();
        await Append(events, stream, 2);

        // Act
        var result = await reader.ReadAtAsync(new SnapshotSelector("tenants", "acme"), realm, capture.Cut);

        // Assert
        Assert.Equal([0UL, 1UL], result.Facts.Select(fact => fact.Locator.ResourcePosition));
        Assert.All(result.Facts, fact => Assert.True(fact.Locator.GlobalPosition < capture.Cut.GlobalWatermarkExclusive));
        Assert.True(result.IsComplete);
    }

    static async Task Append(IEventStore events, EventStreamAddress stream, ulong expected)
    {
        var ev = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Acme", "acme");
        ev.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), expected + 1,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(stream, expected, [ev]);
    }
}
