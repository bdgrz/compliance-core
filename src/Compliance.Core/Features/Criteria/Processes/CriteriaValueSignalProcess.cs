using System.Globalization;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>ProcessSweep definition for independently retryable criteria value proposal signals.</summary>
public static class CriteriaValueSignalProcess
{
    /// <summary>Creates the code-side definition to register with the process catalog.</summary>
    public static ProcessDefinition Definition() => new(CriteriaValueSignal.ProcessName, [], true,
        [new ProcessStageDefinition("reconcile", ["criteria-value-signal"], PerTarget: false)], [], CreateReceiverAsync);

    static ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (step.Record.Key != ProcessKey.ForRoot(CriteriaValueSignal.ProcessName, step.Record.Source.EventId, step.Record.Source) ||
            step.Record.Episode != CriteriaValueSignal.Episode || step.Record.Intent.Schema != CriteriaValueSignal.Schema ||
            step.Record.Intent.ContentId != AggregateIdentity.Create("CriteriaValueSignalIntent",
                step.Record.Source.Stream.ToString(), step.Record.Source.EventId) ||
            step.Record.Intent.MediaType != "application/octet-stream" || step.Record.Targets.Count != 1 ||
            !ulong.TryParse(step.Record.Targets[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out var position))
            throw new InvalidOperationException("The criteria value proposal signal has an invalid source position.");
        return ValueTask.FromResult<IRequest<ReceiverOutcome>?>(new ReconcileCriteriaValueSignal(
            step.Record.Source, position, step.Record.Intent));
    }
}
