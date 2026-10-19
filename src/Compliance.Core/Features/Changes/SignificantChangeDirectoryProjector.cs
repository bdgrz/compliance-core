using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed partial class SignificantChangeDirectoryProjector(
    ISignificantChangeDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), ProjectorName),
      IProjectorHandler<SignificantChangeRecorded>,
      IProjectorHandler<SignificantChangeRevised>,
      IProjectorHandler<SignificantChangeImpactAssessed>,
      IProjectorHandler<ImpactReviewWorkLinked>,
      IProjectorHandler<SignificantChangeSystemDescriptionDecisionRecorded>
{
    public const string ProjectorName = "SignificantChangeDirectoryV1";

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
