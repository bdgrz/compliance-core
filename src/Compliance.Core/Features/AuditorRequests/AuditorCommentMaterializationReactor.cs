using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Starts <c>IndependentChildMaterialization</c> from a committed comment admission. The process record exists before
///     the source checkpoint advances and the sweep resumes it if this reactor stops, so a comment thread is always
///     materialized exactly once.
/// </summary>
sealed partial class AuditorCommentMaterializationReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AuditorRequest.Area), WorkloadName),
        IReactorHandler<AuditorCommentAdmitted>
{
    public const string WorkloadName = "AuditorCommentMaterializationV1";

    public async ValueTask HandleAsync(IReactorContext<AuditorCommentAdmitted> context, CancellationToken ct) =>
        _ = await process.StartAsync(FindingClosureEffects.MaterializationProcessName, context, context.Trigger.Intent,
            [context.Trigger.Operation.LogicalId.ToString()], $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}
