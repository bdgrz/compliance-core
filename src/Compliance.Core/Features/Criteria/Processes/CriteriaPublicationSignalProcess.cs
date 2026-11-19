using System.Globalization;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>ProcessSweep definition for independently retryable criteria publication signals.</summary>
public static class CriteriaPublicationSignalProcess
{
    /// <summary>Creates the code-side definition to register with the process catalog.</summary>
    public static ProcessDefinition Definition() => new(CriteriaPublicationSignal.ProcessName, [], true,
        [new ProcessStageDefinition("reconcile", ["criteria-publication-signal"], PerTarget: false)], [], CreateReceiverAsync);

    static ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (step.Record.Key != ProcessKey.ForRoot(CriteriaPublicationSignal.ProcessName, step.Record.Source.EventId, step.Record.Source) ||
            step.Record.Episode != CriteriaPublicationSignal.Episode || step.Record.Intent.Schema != CriteriaPublicationSignal.Schema ||
            step.Record.Intent.ContentId != AggregateIdentity.Create("CriteriaPublicationSignalIntent",
                step.Record.Source.Stream.ToString(), step.Record.Source.EventId) ||
            step.Record.Intent.MediaType != "application/octet-stream" || step.Record.Targets.Count != 1 ||
            !ulong.TryParse(step.Record.Targets[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out var position))
            throw new InvalidOperationException("The criteria publication signal has an invalid source position.");
        return ValueTask.FromResult<IRequest<ReceiverOutcome>?>(new ReconcileCriteriaPublicationSignal(
            step.Record.Source, position, step.Record.Intent));
    }
}
