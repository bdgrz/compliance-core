using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

public sealed class FitzSourceFactResolverTests
{
    static readonly EventStreamAddress Stream = new("tenant-a", "tenants", "acme");

    [Fact]
    public async Task ShouldResolveExactRealmAndResourceGivenGlobalPositionsRequireGlobalSelector()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var expected = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Acme", "acme");
        expected.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2,
            DateTimeOffset.UtcNow));
        var bytes = serializer.Serialize(expected);
        var other = new StreamRecord("stream://tenant-b/tenants/acme", 1, null, 2, 7, bytes, null, 0);
        var wanted = new StreamRecord(Stream.ToString(), 1, null, 3, 12, bytes, null, 0);
        var streams = new ResolverStreamClient((route, start, _, _) => route == Stream.ToString()
            ? Page(new StreamRecord(Stream.ToString(), 1, bytes), false)
            : start == 0 ? Page(other, true) : Page(wanted, false));
        var resolver = new FitzSourceFactResolver(streams, serializer);

        // Act
        var fact = await resolver.ResolveAsync(Stream, 1);

        // Assert
        Assert.Equal(expected.Metadata!.EventId, fact.Locator.EventId);
        Assert.Equal(Stream, fact.Locator.Source);
        Assert.Equal(1UL, fact.Locator.ResourcePosition);
        Assert.Equal(3UL, fact.Locator.RealmPosition);
        Assert.Equal(12UL, fact.Locator.GlobalPosition);
        Assert.All(streams.Requests, request => Assert.Equal("stream://*/tenants/acme", request.Route));
        Assert.Equal([0UL, 8UL], streams.Requests.Select(request => request.Start));
        Assert.Equal(11UL, streams.Requests[1].Fingerprint);
        Assert.Equal(100UL, streams.Requests[1].Watermark);
    }

    [Fact]
    public async Task ShouldRejectMissingSourceGivenMatchingOffsetOnlyInAnotherRealm()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var streams = new ResolverStreamClient((_, _, _, _) => Page(
            new StreamRecord("stream://tenant-b/tenants/acme", 1, null, 2, 7, Array.Empty<byte>(), null, 0), false));
        var resolver = new FitzSourceFactResolver(streams, provider.GetRequiredService<IDomainEventSerializer>());

        // Act
        var failure = await Record.ExceptionAsync(() => resolver.ResolveAsync(Stream, 1));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    static StreamReadPage Page(StreamRecord record, bool more) => new(
        [new StreamReadItem(record.Route, StreamReadItemKind.Event, record, 0, 0, 0, null)],
        new StreamReadCursor(record.Offset, null, record.RealmOffset, record.GlobalOffset, 11, 100, more));
}
