using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Projections;
namespace Bdgrz.Compliance.Features.Examinations.Projectors;

public sealed partial class ExaminationEngagementDirectoryProjector(
    IExaminationEngagementDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("examinations"),
        FitzExaminationEngagementDirectory.ProjectorName),
      IProjectorHandler<ExaminationEngagementOpened>,
      IProjectorHandler<ExaminationEngagementRevised>,
      IProjectorHandler<TypeIBaselineFrozen>,
      IProjectorHandler<TypeIBaselineAmended>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ExaminationEngagementOpened> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ExaminationEngagementRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TypeIBaselineFrozen> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TypeIBaselineAmended> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
