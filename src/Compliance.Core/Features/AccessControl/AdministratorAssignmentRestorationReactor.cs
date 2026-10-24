using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Starts the transition when the restoration assigns the administrators team (ApplyAdministratorAssignmentRestoration).</summary>
public sealed partial class AdministratorAssignmentRestorationReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-manager-guard"), "AdministratorAssignmentRestorationV1"),
      IReactorHandler<TenantManagerRestorationAccepted>
{
    public ValueTask HandleAsync(IReactorContext<TenantManagerRestorationAccepted> context, CancellationToken ct) =>
        AdministratorTransitionReactions.StartAsync(process, content, context, context.Trigger,
            static kind => kind is AdministratorTransitionKind.RestoreAdministratorsAssignment or AdministratorTransitionKind.RegisterAdministratorsAssignment, "AdministratorAssignmentRestorationReactor", ct);
}
