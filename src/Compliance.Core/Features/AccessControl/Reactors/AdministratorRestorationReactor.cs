using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

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
