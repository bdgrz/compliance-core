using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
namespace Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;

public sealed class FitzAuthoritativeSnapshotReaderTests
{
    const string Realm = "tenant-a";
    static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly SnapshotSelector Tenants = new("tenants");
    static readonly SnapshotSelector Other = new("tenants", "acme");

    [Fact]
    public void ShouldUseCanonicalRouteGivenGlobalResourceSelector()
    {
        // Arrange
        var selector = new SnapshotSelector("tenants", "acme");

        // Act
        var route = selector.Route;

        // Assert
        Assert.Equal("stream://*/tenants/acme", route);
    }

    [Fact]
    public async Task ShouldExcludePublicationGivenAppendAfterCut()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        Append(streams, serializer, Realm, "acme", 0);
        Append(streams, serializer, Realm, "beta", 0);
        var capture = await reader.CaptureAsync();
        Append(streams, serializer, Realm, "late", 0);

        // Act
        var result = await reader.ReadAtAsync(Tenants, Realm, capture.Cut);

        // Assert
        Assert.Equal(2UL, capture.Cut.GlobalWatermarkExclusive);
        Assert.Equal([0UL, 1UL], result.Facts.Select(fact => fact.Locator.GlobalPosition));
        Assert.DoesNotContain(result.Facts, fact => fact.Locator.Source.Resource == "late");
        Assert.True(result.IsComplete);
        Assert.Equal(2UL, result.Cursor.NextGlobalOffset);
    }

    [Fact]
    public async Task ShouldRejectGivenCursorFromAnotherSelector()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        Append(streams, serializer, Realm, "acme", 0);
        var capture = await reader.CaptureAsync();
        var first = await reader.ReadAtAsync(Tenants, Realm, capture.Cut);
        var before = streams.Requests.Count;

        // Act
        var failure = await Record.ExceptionAsync(() => reader.ReadAtAsync(Other, Realm, capture.Cut, first.Cursor));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
        Assert.Equal(before, streams.Requests.Count);
    }

    [Fact]
    public async Task ShouldRejectCursorGivenAnotherTenantScope()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        Append(streams, serializer, Realm, "acme", 0);
        Append(streams, serializer, "tenant-b", "acme", 0);
        var capture = await reader.CaptureAsync();
        var first = await reader.ReadAtAsync(Tenants, Realm, capture.Cut);

        // Act
        var failure = await Record.ExceptionAsync(() => reader.ReadAtAsync(Tenants, "tenant-b", capture.Cut,
            first.Cursor));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    [Fact]
    public async Task ShouldRejectCursorGivenAnotherCapturedCut()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        Append(streams, serializer, Realm, "acme", 0);
        var earlier = await reader.CaptureAsync();
        Append(streams, serializer, Realm, "acme", 1);
        var later = await reader.CaptureAsync();
        var first = await reader.ReadAtAsync(Tenants, Realm, later.Cut);

        // Act
        var failure = await Record.ExceptionAsync(() => reader.ReadAtAsync(Tenants, Realm, earlier.Cut,
            first.Cursor));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    [Fact]
    public async Task ShouldReadCompleteOwnScopeGivenUnrelatedRealmHistoryWasRetainedAway()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        streams.Append(new EventStreamAddress("tenant-b", "tenants", "acme"), Array.Empty<byte>(), skipResource: 3);
        Append(streams, serializer, Realm, "acme", 0);
        var capture = await reader.CaptureAsync();

        // Act
        var result = await reader.ReadAtAsync(Tenants, Realm, capture.Cut);

        // Assert
        Assert.True(result.IsComplete);
        Assert.Equal(Realm, Assert.Single(result.Facts).Locator.Source.Realm);
        Assert.Equal(1, serializer.Deserializations);
    }

    [Fact]
    public async Task ShouldReadCompleteOwnScopeGivenFilteredRecordBelongsToAnotherRealm()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        streams.Append(new EventStreamAddress("tenant-b", "tenants", "acme"), Array.Empty<byte>(), filtered: true);
        Append(streams, serializer, Realm, "acme", 0);
        var capture = await reader.CaptureAsync();

        // Act
        var result = await reader.ReadAtAsync(Tenants, Realm, capture.Cut);

        // Assert
        Assert.True(result.IsComplete);
        Assert.Equal(Realm, Assert.Single(result.Facts).Locator.Source.Realm);
        Assert.Equal(1, serializer.Deserializations);
    }

    [Fact]
    public async Task ShouldUseFreshSnapshotPerSelectorGivenTwoSelectors()
    {
        // Arrange
        var (reader, streams, serializer) = Create(pageSize: 1);
        Append(streams, serializer, Realm, "acme", 0);
        Append(streams, serializer, Realm, "acme", 1);
        var capture = await reader.CaptureAsync();

        // Act
        await reader.ReadAtAsync(Tenants, Realm, capture.Cut);
        await reader.ReadAtAsync(Other, Realm, capture.Cut);

        // Assert
        var anchor = streams.Requests[0];
        var firstOfEach = streams.Requests.Skip(1).Where(request => request.Fingerprint is null).ToList();
        Assert.Equal("stream://**", anchor.Route);
        Assert.Equal(2, firstOfEach.Count);
        Assert.All(firstOfEach, request => Assert.Null(request.Watermark));
        Assert.All(streams.Requests.Skip(1), request => Assert.StartsWith("stream://*/", request.Route, StringComparison.Ordinal));
        Assert.All(streams.Requests.Skip(1).Where(request => request.Fingerprint is not null),
            request => Assert.Equal(ScriptedStreamClient.FingerprintOf(request.Route, request.Watermark!.Value),
                request.Fingerprint));
    }

    [Fact]
    public async Task ShouldReadNothingGivenEmptyCutThenFirstAppend()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        var capture = await reader.CaptureAsync();
        Append(streams, serializer, Realm, "acme", 0);

        // Act
        var result = await reader.ReadAtAsync(Tenants, Realm, capture.Cut);

        // Assert
        Assert.Equal(0UL, capture.Cut.GlobalWatermarkExclusive);
        Assert.Empty(result.Facts);
        Assert.True(result.IsComplete);
        Assert.Equal(0UL, result.Cursor.NextGlobalOffset);
        Assert.Single(streams.Requests);
    }

    [Fact]
    public async Task ShouldContinuePastPageSizeGivenManyRecords()
    {
        // Arrange
        var (reader, streams, serializer) = Create(pageSize: 2);
        for (ulong i = 0; i < 5; i++)
            Append(streams, serializer, Realm, "acme", i);
        var capture = await reader.CaptureAsync();
        Append(streams, serializer, Realm, "acme", 5);

        // Act
        var result = await reader.ReadAtAsync(Other, Realm, capture.Cut);

        // Assert
        var pages = streams.Requests.Skip(1).ToList();
        Assert.Equal([0UL, 1UL, 2UL, 3UL, 4UL], result.Facts.Select(fact => fact.Locator.ResourcePosition));
        Assert.Equal([0UL, 2UL, 4UL], pages.Select(request => request.Start));
        Assert.Null(pages[0].Fingerprint);
        Assert.All(pages.Skip(1), request => Assert.Equal(6UL, request.Watermark));
        Assert.All(pages.Skip(1), request => Assert.NotNull(request.Fingerprint));
        Assert.Equal(5UL, result.Cursor.NextGlobalOffset);
    }

    [Fact]
    public async Task ShouldResumeGivenPersistedCursor()
    {
        // Arrange
        var (reader, streams, serializer) = Create(pageSize: 2);
        for (ulong i = 0; i < 4; i++)
            Append(streams, serializer, Realm, "acme", i);
        var capture = await reader.CaptureAsync();
        var full = await reader.ReadAtAsync(Other, Realm, capture.Cut);
        var resume = full.Cursor with { NextGlobalOffset = 2 };

        // Act
        var result = await reader.ReadAtAsync(Other, Realm, capture.Cut, resume);

        // Assert
        Assert.Equal([2UL, 3UL], result.Facts.Select(fact => fact.Locator.GlobalPosition));
    }

    [Fact]
    public async Task ShouldNotDeserializeGivenRecordFromAnotherRealm()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        Append(streams, serializer, "tenant-b", "acme", 0);
        Append(streams, serializer, Realm, "acme", 0);
        Append(streams, serializer, "tenant-b", "beta", 0);
        var capture = await reader.CaptureAsync();

        // Act
        var result = await reader.ReadAtAsync(Tenants, Realm, capture.Cut);

        // Assert
        var fact = Assert.Single(result.Facts);
        Assert.Equal(Realm, fact.Locator.Source.Realm);
        Assert.Equal(1UL, fact.Locator.GlobalPosition);
        Assert.Equal(0UL, fact.Locator.RealmPosition);
        Assert.Equal(1, serializer.Deserializations);
    }

    [Fact]
    public async Task ShouldFailClosedGivenFilteredRecord()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        Append(streams, serializer, Realm, "acme", 0);
        streams.Append(new EventStreamAddress(Realm, "tenants", "acme"), Array.Empty<byte>(), filtered: true);
        var capture = await reader.CaptureAsync();

        // Act
        var failure = await Record.ExceptionAsync(() => reader.ReadAtAsync(Tenants, Realm, capture.Cut));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    [Fact]
    public async Task ShouldFailClosedGivenRetainedAwayRecord()
    {
        // Arrange
        var (reader, streams, serializer) = Create();
        streams.Append(new EventStreamAddress(Realm, "tenants", "acme"), Body(serializer, Realm, "acme", 3), skipResource: 3);
        var capture = await reader.CaptureAsync();

        // Act
        var failure = await Record.ExceptionAsync(() => reader.ReadAtAsync(Tenants, Realm, capture.Cut));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    static (FitzAuthoritativeSnapshotReader Reader, ScriptedStreamClient Streams, CountingSerializer Serializer)
        Create(int pageSize = 100)
    {
        var provider = EventPersistenceFrameTests.Compose();
        var serializer = new CountingSerializer(provider.GetRequiredService<IDomainEventSerializer>());
        var streams = new ScriptedStreamClient { PageSize = pageSize };
        return (new FitzAuthoritativeSnapshotReader(streams, serializer), streams, serializer);
    }

    static void Append(ScriptedStreamClient streams, IDomainEventSerializer serializer, string realm, string resource,
        ulong version) =>
        streams.Append(new EventStreamAddress(realm, "tenants", resource), Body(serializer, realm, resource, version));

    static byte[] Body(IDomainEventSerializer serializer, string realm, string resource, ulong version)
    {
        var aggregate = Uuid.CreateVersion4();
        var ev = new TenantRegistered(aggregate, Uuid.CreateVersion4(), resource, realm);
        ev.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), aggregate, version + 1, Now));
        return serializer.Serialize(ev).ToArray();
    }
}
