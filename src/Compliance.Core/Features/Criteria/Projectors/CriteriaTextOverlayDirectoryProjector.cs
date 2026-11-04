using Cntryl.Portia;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Projections;
namespace Bdgrz.Compliance.Features.Criteria.Projectors;

public sealed partial class CriteriaTextOverlayDirectoryProjector(
    ICriteriaTextOverlayDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(CriteriaTextOverlay.Area),
        FitzCriteriaTextOverlayDirectory.ProjectorName),
    IProjectorHandler<CriteriaTextOverlayEntryRevised>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<CriteriaTextOverlayEntryRevised> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
