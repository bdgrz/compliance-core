using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>
///     Removes a deleted team's members so no orphaned <see cref="TeamMember" /> assignment survives the team
///     itself. Discovery is source-complete: the live assignments come from the authoritative team-member facts, never
///     from a directory row, so a lagging or empty projector cannot hide a member and the checkpoint cannot advance
///     past an unseen one. Every removal names the exact assignment episode it closes; an assignment that a newer
///     episode replaced is left alone.
/// </summary>
public sealed partial class TeamCleanupReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    IDomainEventReader events,
    IAggregateReader reader)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-teams"), "TeamCleanup"),
      IReactorHandler<TeamDeleted>
{
    public async ValueTask HandleAsync(IReactorContext<TeamDeleted> context, CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        var teamId = context.Trigger.TeamId;
        var sourceEventId = context.Trigger.Metadata.EventId;
        var live = await RbacRelationshipSource.LiveMembersAsync(events, tenantId, teamId, ct).ConfigureAwait(false);
        foreach (var assignment in live)
        {
            var removal = await bus.SendAsync(TeamMemberEffects.Remove(sourceEventId, tenantId, teamId, assignment),
                context, ct).ConfigureAwait(false);
            if (removal.IsSuccess)
                continue;
            // A removal that lost to a concurrent removal or a newer assignment episode is settled, not failed.
            var owner = await reader.HydrateAsync(new TeamMember(tenantId, teamId, assignment.MemberId), ct)
                .ConfigureAwait(false);
            if (owner.IsAssigned && owner.AssignmentEventId == assignment.AssignmentEventId)
                throw new ReactionCommandFailedException(removal.Error);
        }
    }
}
