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
