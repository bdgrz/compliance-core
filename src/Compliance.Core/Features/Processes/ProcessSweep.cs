using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>Resumes overdue non-terminal process records of every registered kind once a minute.</summary>
public sealed class ProcessSweep(IServiceScopeFactory scopes, ProcessCatalog catalog, TimeProvider clock)
    : BackgroundService
{
    /// <summary>Interval between sweeps.</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(60);

    /// <summary>Records resumed per kind per sweep.</summary>
    public const int Batch = 100;

    /// <summary>Resumes every overdue record once; exposed for tests and for the timer loop.</summary>
    public async Task<int> SweepOnceAsync(CancellationToken ct)
    {
        var resumed = 0;
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessRecordStore>();
        var driver = scope.ServiceProvider.GetRequiredService<ProcessDriver>();
        var worker = $"sweep:{Environment.MachineName}:{Environment.ProcessId}";
        foreach (var definition in catalog.Definitions)
        {
            var due = await store.ScanPendingAsync(definition.Name, clock.GetUtcNow(), Batch, ct).ConfigureAwait(false);
            foreach (var key in due)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await driver.RunAsync(key, worker, new RequestDispatchContext(RequestActor.System), ct)
                        .ConfigureAwait(false);
                    resumed++;
                }
                catch (ProcessConcurrencyException)
                {
                    // Another worker advanced the record; it is no longer ours to resume.
                }
            }
        }

        return resumed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed sweep is retried on the next tick; records keep their own due times.
            }
        }
    }
}
