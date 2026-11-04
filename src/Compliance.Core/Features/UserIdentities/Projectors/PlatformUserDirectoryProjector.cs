using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Projections;
namespace Bdgrz.Compliance.Features.UserIdentities.Projectors;

public sealed partial class PlatformUserDirectoryProjector(IPlatformUserDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "user-identities"),
        "PlatformUserDirectoryV2"),
      IProjectorHandler<UserIdentityRegistered>, IProjectorHandler<UserIdentityProfileObserved>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<UserIdentityRegistered> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<UserIdentityProfileObserved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
