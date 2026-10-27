using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Starts the transition when the restoration reinstates a member (ApplyMemberRestoration).</summary>
public sealed partial class AdministratorRestorationReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-manager-guard"), "AdministratorRestorationV1"),
      IReactorHandler<TenantManagerRestorationAccepted>
{
    public ValueTask HandleAsync(IReactorContext<TenantManagerRestorationAccepted> context, CancellationToken ct) =>
        AdministratorTransitionReactions.StartAsync(process, content, context, context.Trigger,
            static kind => kind == AdministratorTransitionKind.ReinstateMember, "AdministratorRestorationReactor", ct);
}
