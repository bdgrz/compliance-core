using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>Starts the transition when the guard admitted a neutral administrators-team removal (ApplyNeutralAdministratorAssignmentMutation).</summary>
public sealed partial class AdministratorNeutralAssignmentMutationReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-manager-guard"), "AdministratorNeutralAssignmentMutationV1"),
      IReactorHandler<TenantManagerSourceMutationAdmitted>
{
    public ValueTask HandleAsync(IReactorContext<TenantManagerSourceMutationAdmitted> context, CancellationToken ct) =>
        AdministratorTransitionReactions.StartAsync(process, content, context, context.Trigger,
            static kind => kind == AdministratorTransitionKind.NeutralAssignmentMutation, "AdministratorNeutralAssignmentMutationReactor", ct);
}
