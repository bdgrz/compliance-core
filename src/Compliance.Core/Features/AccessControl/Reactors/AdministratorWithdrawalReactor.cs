using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>Starts the transition when the withdrawal suspends or deprovisions a member (ApplyMemberWithdrawal).</summary>
public sealed partial class AdministratorWithdrawalReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-manager-guard"), "AdministratorWithdrawalV1"),
      IReactorHandler<TenantManagerWithdrawalAccepted>
{
    public ValueTask HandleAsync(IReactorContext<TenantManagerWithdrawalAccepted> context, CancellationToken ct) =>
        AdministratorTransitionReactions.StartAsync(process, content, context, context.Trigger,
            static kind => kind is AdministratorTransitionKind.SuspendMember or AdministratorTransitionKind.DeprovisionMember, "AdministratorWithdrawalReactor", ct);
}
