using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>Starts the transition when the guard admitted a neutral member mutation (ApplyNeutralMemberSourceMutation).</summary>
public sealed partial class AdministratorNeutralMemberMutationReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-manager-guard"), "AdministratorNeutralMemberMutationV1"),
      IReactorHandler<TenantManagerSourceMutationAdmitted>
{
    public ValueTask HandleAsync(IReactorContext<TenantManagerSourceMutationAdmitted> context, CancellationToken ct) =>
        AdministratorTransitionReactions.StartAsync(process, content, context, context.Trigger,
            static kind => kind == AdministratorTransitionKind.NeutralMemberMutation, "AdministratorNeutralMemberMutationReactor", ct);
}
