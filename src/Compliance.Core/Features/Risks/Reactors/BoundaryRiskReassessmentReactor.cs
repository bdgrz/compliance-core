using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Risks.Readers;
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
