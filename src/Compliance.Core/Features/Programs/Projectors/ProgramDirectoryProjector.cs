using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Readiness.Events;
namespace Bdgrz.Compliance.Features.Programs.Projectors;

public sealed partial class ProgramDirectoryProjector(IProgramDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "ProgramDirectory"),
      IProjectorHandler<ProgramCreated>, IProjectorHandler<ProgramRevised>,
      IProjectorHandler<ProgramCriteriaEditionSelected>,
      IProjectorHandler<TypeIEntryDecisionRecorded>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ProgramCreated> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ProgramRevised> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ProgramCriteriaEditionSelected> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TypeIEntryDecisionRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
