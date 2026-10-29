using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>Resumes overdue non-terminal process records of every registered kind once a minute.</summary>
public sealed class ProcessSweep(IServiceScopeFactory scopes, ProcessCatalog catalog, TimeProvider clock,
    ILogger<ProcessSweep>? logger = null)
    : BackgroundService
{
    static readonly Action<ILogger, string, string, Uuid, Uuid, Exception?> RecordRecoveryFailed =
        LoggerMessage.Define<string, string, Uuid, Uuid>(LogLevel.Warning, new EventId(1, nameof(RecordRecoveryFailed)),
            "Process recovery failed for {ProcessKind} in {Realm} at {RootOperationId}/{SourceId}.");
    static readonly Action<ILogger, Exception?> SweepRecoveryFailed = LoggerMessage.Define(LogLevel.Warning,
        new EventId(2, nameof(SweepRecoveryFailed)), "Process recovery sweep failed and will retry.");

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
                catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    // Leave this record due for retry while allowing independent records in the batch to recover.
                    if (logger is not null)
                        RecordRecoveryFailed(logger, key.ProcessKind, key.Realm, key.RootOperationId, key.SourceId, exception);
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
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // A failed sweep is retried on the next tick; records keep their own due times.
                if (logger is not null)
                    SweepRecoveryFailed(logger, exception);
            }
        }
    }
}
