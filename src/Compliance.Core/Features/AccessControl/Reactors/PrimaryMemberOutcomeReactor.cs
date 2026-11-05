using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

public sealed partial class PrimaryMemberOutcomeReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-members"), "PrimaryMemberOutcomeV1"),
      IReactorHandler<MemberMarkedPrimary>, IReactorHandler<MemberPrimaryMarkRefused>
{
    public ValueTask<Result> HandleAsync(IReactorContext<MemberMarkedPrimary> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        return bus.SendReactionAsync(new RecordPrimaryMarkOutcome(ev.TenantId, ev.UserId, ev.MembershipEpisodeId, ev.DesignationId), context, ct);
    }
    public ValueTask<Result> HandleAsync(IReactorContext<MemberPrimaryMarkRefused> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        return bus.SendReactionAsync(new RecordPrimaryMarkOutcome(ev.TenantId, ev.UserId, ev.MembershipEpisodeId, ev.DesignationId), context, ct);
    }
}
