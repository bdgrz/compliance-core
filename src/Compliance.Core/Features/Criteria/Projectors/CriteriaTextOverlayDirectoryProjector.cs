using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Projectors;

public sealed partial class CriteriaTextOverlayDirectoryProjector(
    ICriteriaTextOverlayDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(CriteriaTextOverlay.Area),
        FitzCriteriaTextOverlayDirectory.ProjectorName),
    IProjectorHandler<CriteriaTextOverlayEntryRevised>
{
    public ValueTask HandleAsync(CriteriaTextOverlayEntryRevised ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
