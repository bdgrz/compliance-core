using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationEvidenceSourceReferenceProjector(
    IApplicationEvidenceSourceReferenceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("evidence-support"),
        FitzApplicationEvidenceSourceReferenceDirectory.ProjectorName),
      IProjectorHandler<EvidenceSupportLinked>
{
    public ValueTask HandleAsync(EvidenceSupportLinked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
