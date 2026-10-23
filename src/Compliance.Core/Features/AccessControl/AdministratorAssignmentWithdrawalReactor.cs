using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Starts the transition when the withdrawal removes an administrators-team assignment (ApplyAdministratorAssignmentWithdrawal).</summary>
public sealed partial class AdministratorAssignmentWithdrawalReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-manager-guard"), "AdministratorAssignmentWithdrawalV1"),
      IReactorHandler<TenantManagerWithdrawalAccepted>
{
    public ValueTask HandleAsync(IReactorContext<TenantManagerWithdrawalAccepted> context, CancellationToken ct) =>
        AdministratorTransitionReactions.StartAsync(process, content, context, context.Trigger,
            static kind => kind == AdministratorTransitionKind.RemoveAdministratorsAssignment, "AdministratorAssignmentWithdrawalReactor", ct);
}
