using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
namespace Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;

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
