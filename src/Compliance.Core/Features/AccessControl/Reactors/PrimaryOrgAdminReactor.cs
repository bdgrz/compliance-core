using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

public sealed partial class PrimaryOrgAdminReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("primary-org-admins"), "PrimaryOrgAdminV1"),
      IReactorHandler<PrimaryOrgAdminDesignationRequested>, IReactorHandler<PrimaryOrgAdminReleased>
{
    public ValueTask<Result> HandleAsync(IReactorContext<PrimaryOrgAdminDesignationRequested> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        return bus.SendReactionAsync(new MarkMemberPrimary(ev.TenantId, ev.UserId, ev.MembershipEpisodeId, ev.DesignationId), context, ct);
    }
    public ValueTask<Result> HandleAsync(IReactorContext<PrimaryOrgAdminReleased> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        return bus.SendReactionAsync(new UnmarkMemberPrimary(ev.TenantId, ev.UserId, ev.MembershipEpisodeId, ev.DesignationId), context, ct);
    }
}
