using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Projections;
namespace Bdgrz.Compliance.Features.Programs.Projectors;

public sealed partial class ClientServiceDirectoryProjector(IClientServiceDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "ClientServiceDirectory"),
      IProjectorHandler<ClientServiceCreated>, IProjectorHandler<ClientServiceRevised>,
      IProjectorHandler<ClientServiceRetired>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ClientServiceCreated> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ClientServiceRevised> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ClientServiceRetired> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
