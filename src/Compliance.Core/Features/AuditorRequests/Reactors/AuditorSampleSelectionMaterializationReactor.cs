using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Starts <c>IndependentChildMaterialization</c> from a committed sample selection admission. The process record
///     exists before the source checkpoint advances and the sweep resumes it if this reactor stops, so an admitted
///     selection is materialized exactly once, including after its request resolved.
/// </summary>
sealed partial class AuditorSampleSelectionMaterializationReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AuditorRequest.Area), WorkloadName),
        IReactorHandler<AuditorSampleSelectionAdmitted>
{
    public const string WorkloadName = "AuditorSampleSelectionMaterializationV1";

    public async ValueTask HandleAsync(IReactorContext<AuditorSampleSelectionAdmitted> context, CancellationToken ct)
    {
        await AuditorRequestReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Owner, ct)
            .ConfigureAwait(false);
        _ = await process.StartAsync(FindingClosureEffects.MaterializationProcessName, context, context.Trigger.Intent,
            [context.Trigger.Operation.LogicalId.ToString()], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
