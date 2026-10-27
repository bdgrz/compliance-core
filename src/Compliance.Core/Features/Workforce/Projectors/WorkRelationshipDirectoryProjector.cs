using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Projectors;

public sealed partial class WorkRelationshipDirectoryProjector(
    IWorkRelationshipDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("work-relationships"),
            "WorkRelationshipDirectoryV1"),
      IProjectorHandler<WorkRelationshipRecorded>, IProjectorHandler<WorkRelationshipRevised>
{
    public ValueTask HandleAsync(WorkRelationshipRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(WorkRelationshipRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
