using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Projectors;

public sealed partial class ApplicationEvidenceSourceReferenceProjector(
    IApplicationEvidenceSourceReferenceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("evidence-support"),
        FitzApplicationEvidenceSourceReferenceDirectory.ProjectorName),
      IProjectorHandler<EvidenceSupportLinked>
{
    public ValueTask HandleAsync(EvidenceSupportLinked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
