using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Projections;
namespace Bdgrz.Compliance.Features.Tenants.Projectors;

public sealed partial class TenantInvitationDirectoryProjector(
    ITenantInvitationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("tenant-invitations"),
      "TenantInvitationDirectory"),
      IProjectorHandler<TenantMemberInvited>, IProjectorHandler<TenantInvitationAccepted>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantMemberInvited> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantInvitationAccepted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
