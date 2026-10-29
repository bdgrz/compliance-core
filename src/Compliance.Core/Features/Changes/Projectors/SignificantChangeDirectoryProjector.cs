using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Events;
using Bdgrz.Compliance.Features.Changes.Projections;
namespace Bdgrz.Compliance.Features.Changes.Projectors;

public sealed partial class SignificantChangeDirectoryProjector(
    ISignificantChangeDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(SignificantChange.Area), ProjectorName),
      IProjectorHandler<SignificantChangeRecorded>,
      IProjectorHandler<SignificantChangeRevised>,
      IProjectorHandler<SignificantChangeImpactAssessed>,
      IProjectorHandler<ImpactReviewWorkLinked>,
      IProjectorHandler<SignificantChangeSystemDescriptionDecisionRecorded>
{
    public const string ProjectorName = "SignificantChangeDirectoryV2";

    public ValueTask HandleAsync(SignificantChangeRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SignificantChangeRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SignificantChangeImpactAssessed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ImpactReviewWorkLinked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SignificantChangeSystemDescriptionDecisionRecorded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
