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
///     Removes a deleted role's permission assignments and team assignments so no orphaned
///     <see cref="RolePermission" />/<see cref="TeamRole" /> assignment survives the role itself. Discovery is
///     source-complete: the live relationships come from the authoritative assignment facts, never from a directory
///     row, so a lagging or empty projector cannot hide one and the checkpoint cannot advance past an unseen one.
///     Every removal names the exact assignment episode and revision it closes; an assignment that a newer episode
///     replaced is left alone.
/// </summary>
public sealed partial class RoleCleanupReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    IDomainEventReader events,
    IAggregateReader reader)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-roles"), "RoleCleanup"),
      IReactorHandler<RoleDeleted>
{
    public async ValueTask HandleAsync(IReactorContext<RoleDeleted> context, CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        var roleId = context.Trigger.RoleId;
        var sourceEventId = context.Trigger.Metadata.EventId;

        foreach (var permission in await RbacRelationshipSource.LivePermissionsAsync(events, tenantId, roleId, ct)
                     .ConfigureAwait(false))
        {
            var removal = await bus.SendAsync(RbacPermissionEffects.Remove(sourceEventId, tenantId, roleId, permission),
                context, ct).ConfigureAwait(false);
            if (removal.IsSuccess)
                continue;
            var owner = await reader.HydrateAsync(new RolePermission(tenantId, roleId, permission.Permission), ct)
                .ConfigureAwait(false);
            if (owner.AssignmentId == permission.AssignmentId)
                throw new ReactionCommandFailedException(removal.Error);
        }

        foreach (var team in await RbacRelationshipSource.LiveTeamsAsync(events, tenantId, roleId, ct)
                     .ConfigureAwait(false))
        {
            var removal = await bus.SendAsync(RbacTeamRoleEffects.Remove(sourceEventId, tenantId, roleId, team),
                context, ct).ConfigureAwait(false);
            if (removal.IsSuccess)
                continue;
            var owner = await reader.HydrateAsync(new TeamRole(tenantId, team.TeamId, roleId), ct)
                .ConfigureAwait(false);
            if (owner.AssignmentId == team.AssignmentId)
                throw new ReactionCommandFailedException(removal.Error);
        }
    }
}
