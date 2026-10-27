using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>
///     Starts <c>ClientIndependencePreviewProcess</c> from a committed evaluation admission. The process record, with
///     its frozen intent, exists before the source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class ClientIndependencePreviewReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, ISourceFactResolver sources, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ClientServiceIndependence.Area), WorkloadName),
        IReactorHandler<ClientIndependenceEvaluationAdmitted>
{
    public const string WorkloadName = "ClientIndependencePreviewV1";

    public async ValueTask HandleAsync(IReactorContext<ClientIndependenceEvaluationAdmitted> context,
        CancellationToken ct)
    {
        await ClientServiceSource.VerifyAsync(sources, content, context.Source.Stream, context.Source.ResourceOffset,
            context.Trigger, ct).ConfigureAwait(false);
        _ = await process.StartAsync(ClientIndependencePreviewEffects.ProcessName, context, context.Trigger.Intent,
            [], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
