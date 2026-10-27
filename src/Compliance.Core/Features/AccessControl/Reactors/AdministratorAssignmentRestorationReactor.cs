using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

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
