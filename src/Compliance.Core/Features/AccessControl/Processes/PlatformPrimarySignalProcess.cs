using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Processes;

/// <summary>Existing process-store definition for retrying one primary participant attempt.</summary>
public static class PlatformPrimarySignalProcess
{
    /// <summary>Returns the opt-in process definition.</summary>
    public static ProcessDefinition Definition() => new(PlatformPrimarySignalIntake.ProcessName, [], true,
        [new ProcessStageDefinition("reconcile", ["platform-primary-participant"], PerTarget: false)], [], CreateAsync);

    static ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep step, IServiceProvider services, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var record = step.Record;
        if (record.Key != ProcessKey.ForRoot(PlatformPrimarySignalIntake.ProcessName, record.Source.EventId, record.Source) ||
            record.Episode != PlatformPrimarySignalIntake.ProcessName || record.Intent.Schema != PlatformPrimarySignalIntake.Schema ||
            record.Intent.MediaType != "application/octet-stream" || record.Intent.ContentId != AggregateIdentity.Create(
                "PlatformPrimarySignalIntent", record.Source.Stream.ToString(), record.Source.EventId) || record.Targets.Count != 1 ||
            !PlatformPrimarySignalIntake.TrySlot(record.Targets[0], out var source, out var target, out var revision))
            throw new InvalidOperationException("The primary participant attempt has an invalid frozen intent.");
        return ValueTask.FromResult<IRequest<ReceiverOutcome>?>(new ReconcilePlatformPrimarySignal(record.Source,
            record.Intent, source, target, revision));
    }
}
