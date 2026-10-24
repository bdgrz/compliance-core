using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Processes;

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
