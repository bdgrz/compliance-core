using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Evidence.Events;
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
