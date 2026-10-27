using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public sealed partial class ExaminationEngagementDirectoryProjector(
    IExaminationEngagementDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("examinations"),
        FitzExaminationEngagementDirectory.ProjectorName),
      IProjectorHandler<ExaminationEngagementOpened>,
      IProjectorHandler<ExaminationEngagementRevised>,
      IProjectorHandler<TypeIBaselineFrozen>,
      IProjectorHandler<TypeIBaselineAmended>
{
    public ValueTask HandleAsync(ExaminationEngagementOpened ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ExaminationEngagementRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TypeIBaselineFrozen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TypeIBaselineAmended ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
