using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Tenants.Events;
namespace Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;

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
        var streams = new ResolverStreamClient((route, _) => route == Stream.ToString()
            ? [Batch(new StreamRecord(Stream.ToString(), 1, bytes), 0, true)]
            : [Batch(other, 0, false), Batch(wanted, 8, true)]);
        var resolver = new FitzSourceFactResolver(streams, serializer);

        // Act
        var fact = await resolver.ResolveAsync(Stream, 1);

        // Assert
        Assert.Equal(expected.Metadata!.EventId, fact.Locator.EventId);
        Assert.Equal(Stream, fact.Locator.Source);
        Assert.Equal(1UL, fact.Locator.ResourcePosition);
        Assert.Equal(3UL, fact.Locator.RealmPosition);
        Assert.Equal(12UL, fact.Locator.GlobalPosition);
        var request = Assert.Single(streams.Requests);
        Assert.Equal("stream://*/tenants/acme", request.Route);
        Assert.Equal(0UL, request.Start);
        Assert.Equal(StreamReadMode.Replay, request.Mode);
    }

    [Fact]
    public async Task ShouldRejectMissingSourceGivenMatchingOffsetOnlyInAnotherRealm()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var streams = new ResolverStreamClient((_, _) => [Batch(
            new StreamRecord("stream://tenant-b/tenants/acme", 1, null, 2, 7, Array.Empty<byte>(), null, 0), 0, true)]);
        var resolver = new FitzSourceFactResolver(streams, provider.GetRequiredService<IDomainEventSerializer>());

        // Act
        var failure = await Record.ExceptionAsync(() => resolver.ResolveAsync(Stream, 1));

        // Assert
        Assert.IsType<SourceFactUnavailableException>(failure);
    }

    static StreamReadBatch Batch(StreamRecord record, ulong from, bool caughtUp) => new(
        [new StreamReadItem(record.Route, StreamReadItemKind.Event, record)], from,
        checked(record.GlobalOffset!.Value + 1), caughtUp);
}
