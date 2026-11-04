using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class AccessGrantProjector(IAccessGrantProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "AccessGrantsV1"),
      IProjectorHandler<AccessGrantIssued>, IProjectorHandler<AccessGrantRevoked>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessGrantIssued> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessGrantRevoked> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
