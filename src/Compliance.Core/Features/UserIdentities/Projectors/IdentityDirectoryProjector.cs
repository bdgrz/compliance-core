using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Projections;
namespace Bdgrz.Compliance.Features.UserIdentities.Projectors;

public sealed partial class IdentityDirectoryProjector(IUserIdentityDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "user-identities"),
        "UserIdentityDirectory"),
      IProjectorHandler<UserIdentityRegistered>,
      IProjectorHandler<UserIdentityAuthenticated>,
      IProjectorHandler<UserIdentityRevoked>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<UserIdentityRegistered> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<UserIdentityAuthenticated> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<UserIdentityRevoked> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
