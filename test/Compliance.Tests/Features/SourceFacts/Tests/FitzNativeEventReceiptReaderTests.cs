using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
namespace Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;

public sealed class FitzNativeEventReceiptReaderTests
{
    static readonly EventStreamAddress Stream = new("receipt-tenant", "tenants", "acme");
    static readonly DateTimeOffset Receipt = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public async Task ShouldReturnVerifiedBrokerMillisecondsGivenOccurrenceTimePrecedesCommit()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var value = Source();
        var raw = Raw(serializer.Serialize(value));
        var client = new ResolverStreamClient((_, _, _, _) => Page(raw));
        var reader = new FitzNativeEventReceiptReader(client, serializer);

        // Act
        var actual = await reader.RecordedAtAsync(new(Stream, value, 1, EventCursor.Start));

        // Assert
        Assert.Equal(Receipt, actual);
        Assert.NotEqual(value.Metadata.OccurredOn, actual);
        var request = Assert.Single(client.Requests);
        Assert.Equal(Stream.ToString(), request.Route);
        Assert.Equal(1UL, request.Start);
    }
    [Theory]
    [InlineData("route")]
    [InlineData("offset")]
    [InlineData("event_id")]
    [InlineData("aggregate_version")]
    [InlineData("aggregate_id")]
    [InlineData("occurrence_metadata")]
    [InlineData("payload")]
    [InlineData("timestamp_absent")]
    [InlineData("timestamp_overflow")]
    public async Task ShouldRefuseReceiptGivenExactNativeTupleOrPayloadDiffers(string changed)
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var value = Source();
        var actual = value with { Name = changed == "payload" ? "Different owner payload" : value.Name };
        actual.AttachMetadata(value.Metadata with
        {
            EventId = changed == "event_id" ? Uuid.CreateVersion4() : value.Metadata.EventId,
            AggregateVersion = changed == "aggregate_version" ? 3UL : value.Metadata.AggregateVersion,
            AggregateId = changed == "aggregate_id" ? Uuid.CreateVersion4() : value.Metadata.AggregateId,
            OccurredOn = changed == "occurrence_metadata" ? value.Metadata.OccurredOn.AddSeconds(1) : value.Metadata.OccurredOn,
        });
        var raw = Raw(serializer.Serialize(actual)) with
        {
            Route = changed == "route" ? "stream://foreign-tenant/tenants/acme" : Stream.ToString(),
            Offset = changed == "offset" ? 2UL : 1UL,
            Timestamp = changed == "timestamp_absent" ? 0UL : changed == "timestamp_overflow" ? ulong.MaxValue : checked((ulong)Receipt.ToUnixTimeMilliseconds()),
        };
        var reader = new FitzNativeEventReceiptReader(new ResolverStreamClient((_, _, _, _) => Page(raw)), serializer);

        // Act
        var failure = await Record.ExceptionAsync(async () => await reader.RecordedAtAsync(new(Stream, value, 1, EventCursor.Start)));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }
    [Fact]
    public async Task ShouldRemainPendingGivenExactBrokerReceiptIsAbsent()
    {
        // Arrange
        await using var provider = EventPersistenceFrameTests.Compose();
        var client = new ResolverStreamClient((_, _, _, _) => new StreamReadPage([], new(0, null, null, null, 11, 100, false)));
        var reader = new FitzNativeEventReceiptReader(client, provider.GetRequiredService<IDomainEventSerializer>());

        // Act
        var failure = await Record.ExceptionAsync(async () => await reader.RecordedAtAsync(new(Stream, Source(), 1, EventCursor.Start)));

        // Assert
        Assert.IsType<ImmutableContentUnavailableException>(failure);
    }
    static TenantRegistered Source()
    {
        var value = new TenantRegistered(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Acme", "acme");
        value.AttachMetadata(new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, Receipt.AddDays(-10)));
        return value;
    }
    static StreamRecord Raw(ReadOnlyMemory<byte> bytes) => new(Stream.ToString(), 1, null, null, null, bytes, null, checked((ulong)Receipt.ToUnixTimeMilliseconds()));
    static StreamReadPage Page(StreamRecord value) => new([new(value.Route, StreamReadItemKind.Event, value, 0, 0, 0, null)], new(value.Offset, null, null, null, 11, 100, false));
}
