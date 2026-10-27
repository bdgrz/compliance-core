using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Reactors;

/// <summary>
///     Raises M0-D10 boundary-change reassessment triggers when a successor boundary version is approved, one command
///     per assessed risk of the program. The initial approval defines scope rather than changing it, so it is skipped.
/// </summary>
public sealed partial class BoundaryRiskReassessmentReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    IAggregateReader reader,
    IRiskDraftDirectoryReader risks,
    RiskDraftListReadConsistency consistency)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("boundaries"), WorkloadName),
      IReactorHandler<BoundaryApproved>
{
    public const string WorkloadName = "BoundaryRiskReassessmentV1";

    public async ValueTask HandleAsync(IReactorContext<BoundaryApproved> context,
        CancellationToken ct)
    {
        var approved = context.Trigger;
        var boundary = await reader.HydrateAsync(new SystemBoundary(approved.TenantId,
            approved.BoundaryId), ct).ConfigureAwait(false);
        if (!boundary.IsCreated || boundary.FirstApprovedVersionId == approved.DraftVersionId)
            return;
        await RiskReassessmentFanOut.SendAsync(bus, reader, risks, consistency, context, approved.Metadata.EventId,
            approved.TenantId, boundary.ProgramId, RiskStates.BoundaryChanged, approved.DraftVersionId.ToString(),
            approved.DecidedAt, null, ct).ConfigureAwait(false);
    }
}
