using Bdgrz.Compliance.Features.Processes;
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
}
