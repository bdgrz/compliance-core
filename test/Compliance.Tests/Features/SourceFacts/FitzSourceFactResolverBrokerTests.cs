using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class FitzSourceFactResolverBrokerTests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldReportDistinctRealmAndGlobalOffsetsGivenResourceRead()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var client = factory.CreateClient();
        var realm = $"k2-{Guid.NewGuid():N}";
        var stream = new EventStreamAddress(realm, "tenants", "acme");
        var other = new EventStreamAddress(realm, "tenants", "other");
        var events = factory.Services.GetRequiredService<IEventStore>();
        var now = DateTimeOffset.UtcNow;
        await Append(events, other, Uuid.CreateVersion4(), now);
        await Append(events, stream, Uuid.CreateVersion4(), now);
        await Append(events, stream, Uuid.CreateVersion4(), now, 1);
        await using var fitz = new Client(new ClientConfig(new Uri(broker.WebSocketEndpoint, UriKind.Absolute),
            Timeout: TimeSpan.FromSeconds(10)));
        await fitz.ConnectWhenReadyAsync(new ConnectWhenReadyOptions(TimeSpan.FromSeconds(15)));
        var resolver = new FitzSourceFactResolver(fitz.Stream,
            factory.Services.GetRequiredService<IDomainEventSerializer>());

        // Act
        var first = await resolver.ResolveAsync(stream, 0);
        var second = await resolver.ResolveAsync(stream, 1);

        // Assert
        Assert.Equal(0UL, first.Locator.ResourcePosition);
        Assert.Equal(1UL, second.Locator.ResourcePosition);
        Assert.True(second.Locator.RealmPosition > first.Locator.RealmPosition);
        Assert.True(second.Locator.GlobalPosition > first.Locator.GlobalPosition);
        Assert.NotEqual(first.Locator.ResourcePosition, first.Locator.RealmPosition);
        Assert.True(first.Locator.GlobalPosition >= first.Locator.RealmPosition);
    }

    static async Task Append(IEventStore events, EventStreamAddress stream, Uuid id, DateTimeOffset now,
        ulong expected = 0)
    {
        var ev = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Acme", "acme");
        ev.AttachMetadata(new DomainEventMetadata(id, Uuid.CreateVersion4(), expected + 1, now));
        await events.AppendAsync(stream, expected, [ev]);
    }
}
