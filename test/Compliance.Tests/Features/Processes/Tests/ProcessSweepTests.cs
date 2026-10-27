using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
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
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Processes.Tests;

public sealed class ProcessSweepTests
{
    [Fact]
    public async Task ShouldResumeOverdueRecordsGivenSweep()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        await host.StartAsync("t1");
        var sweep = host.Provider.GetRequiredService<ProcessSweep>();

        // Act
        var resumed = await sweep.SweepOnceAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, resumed);
        Assert.Equal(ProcessState.Completed, (await host.Store.ReadAsync(ProcessTestHost.Key))!.State);
    }

    [Fact]
    public async Task ShouldResumeHealthyProcessGivenEarlierDueRecordCannotBeRead()
    {
        // Arrange
        await using var host = new ProcessTestHost(inner => new UnreadableFirstStore(inner));
        await host.StartAsync("t1");
        var source = ProcessTestHost.Source with { EventId = Uuid.CreateVersion4() };
        var root = Uuid.Parse("00000000-0000-4000-8000-0000000000d3",
            System.Globalization.CultureInfo.InvariantCulture);
        var key = ProcessKey.ForRoot(ProcessTestHost.Process, root, source);
        await host.Store.CreateIfAbsentAsync(ProcessRecord.Start(key, source, "initial", ProcessTestHost.Intent,
            ["t2"], host.Clock.GetUtcNow()));
        var sweep = host.Provider.GetRequiredService<ProcessSweep>();

        // Act
        var resumed = await sweep.SweepOnceAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, resumed);
        Assert.Equal(ProcessState.Completed, (await host.Store.ReadAsync(key))!.State);
    }

    [Fact]
    public async Task ShouldLeaveFutureRecordsGivenSweepBeforeDue()
    {
        // Arrange
        await using var host = new ProcessTestHost();
        var record = await host.StartAsync("t1");
        await host.Store.ReplaceAsync(record with { NextDueAt = host.Clock.GetUtcNow().AddMinutes(5) }, record.Version);
        var sweep = host.Provider.GetRequiredService<ProcessSweep>();

        // Act
        var resumed = await sweep.SweepOnceAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, resumed);
        Assert.Empty(host.Recorder.Calls);
    }

    sealed class UnreadableFirstStore(IProcessRecordStore inner) : IProcessRecordStore
    {
        public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) =>
            key == ProcessTestHost.Key ? throw new TimeoutException("The first record cannot be read.")
                : inner.ReadAsync(key, ct);

        public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
            inner.CreateIfAbsentAsync(record, ct);

        public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default) =>
            inner.ReplaceAsync(record, expectedVersion, ct);

        public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore,
            int limit, CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
    }
}
