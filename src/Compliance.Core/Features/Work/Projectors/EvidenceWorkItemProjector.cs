using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class EvidenceWorkItemProjector(IEvidenceWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("evidence-requests"),
        FitzEvidenceWorkItemDirectory.ProjectorName),
      IProjectorHandler<EvidenceRequestOpened>, IProjectorHandler<EvidenceRequestFulfilled>,
      IProjectorHandler<EvidenceRequestCancelled>
{
    public ValueTask HandleAsync(EvidenceRequestOpened ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(EvidenceRequestFulfilled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(EvidenceRequestCancelled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
