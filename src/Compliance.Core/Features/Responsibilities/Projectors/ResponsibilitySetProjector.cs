using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Events;
namespace Bdgrz.Compliance.Features.Responsibilities.Projectors;

public sealed partial class ResponsibilitySetProjector(IResponsibilitySetProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "ResponsibilitySetsV1"),
      IProjectorHandler<ResponsibilityAssigned>, IProjectorHandler<ResponsibilityRevoked>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ResponsibilityAssigned> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ResponsibilityRevoked> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
