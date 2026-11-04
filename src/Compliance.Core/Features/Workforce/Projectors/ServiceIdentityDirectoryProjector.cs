using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Projectors;

public sealed partial class ServiceIdentityDirectoryProjector(
    IServiceIdentityDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("service-identities"),
            "ServiceIdentityDirectoryV1"),
      IProjectorHandler<ServiceIdentityRecorded>, IProjectorHandler<ServiceIdentityRevised>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ServiceIdentityRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ServiceIdentityRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
