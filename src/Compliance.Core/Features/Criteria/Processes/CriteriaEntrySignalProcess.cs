using System.Globalization;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Independent retry process for one structural proposal.</summary>
public static class CriteriaEntrySignalProcess
{
    public static ProcessDefinition Definition() => new(CriteriaEntrySignal.ProcessName, [], true,
        [new ProcessStageDefinition("reconcile", ["criteria-entry-signal"], PerTarget: false)], [], CreateReceiverAsync);

    static ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (step.Record.Key != ProcessKey.ForRoot(CriteriaEntrySignal.ProcessName, step.Record.Source.EventId, step.Record.Source) ||
            step.Record.Episode != CriteriaEntrySignal.Episode || step.Record.Intent.Schema != CriteriaEntrySignal.Schema ||
            step.Record.Intent.ContentId != AggregateIdentity.Create("CriteriaEntrySignalIntent",
                step.Record.Source.Stream.ToString(), step.Record.Source.EventId) ||
            step.Record.Intent.MediaType != "application/octet-stream" || step.Record.Targets.Count != 1 ||
            !ulong.TryParse(step.Record.Targets[0], NumberStyles.None, CultureInfo.InvariantCulture, out var position))
            throw new InvalidOperationException("The entry proposal signal has an invalid source position.");
        return ValueTask.FromResult<IRequest<ReceiverOutcome>?>(new ReconcileCriteriaEntrySignal(
            step.Record.Source, position, step.Record.Intent));
    }
}
