using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Fitz;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Processes.Tests;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class FitzKvProcessRecordStoreBrokerTests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldPersistAndScanLargeProcessGivenRealBroker()
    {
        // Arrange
        await using var fitz = new Client(new ClientConfig(new Uri(broker.WebSocketEndpoint, UriKind.Absolute),
            Timeout: TimeSpan.FromSeconds(10)));
        await fitz.ConnectWhenReadyAsync(new ConnectWhenReadyOptions(TimeSpan.FromSeconds(15)));
        var store = new FitzKvProcessRecordStore(fitz.Kv);
        var now = DateTimeOffset.UtcNow;
        var key = ProcessTestHost.Key with { ProcessKind = $"BrokerProbe{Guid.NewGuid():N}" };
        var targets = Enumerable.Range(0, 2000).Select(index => $"target-{index:D4}-{new string('x', 64)}").ToArray();
        var pending = await store.CreateIfAbsentAsync(ProcessRecord.Start(key, ProcessTestHost.Source, "initial",
            ProcessTestHost.Intent, targets, now));

        // Act
        var read = await store.ReadAsync(key);
        var due = await store.ScanPendingAsync(key.ProcessKind, now, 100);
        await store.ReplaceAsync(pending with { State = ProcessState.Completed }, pending.Version);
        var after = await store.ScanPendingAsync(key.ProcessKind, now, 100);

        // Assert
        Assert.Equal(pending, read);
        Assert.Equal([key], due);
        Assert.Empty(after);
    }
}
