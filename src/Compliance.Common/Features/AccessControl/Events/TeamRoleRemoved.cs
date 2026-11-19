using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.team-role.removed", 1)]
public sealed record TeamRoleRemoved(Uuid TenantId, Uuid TeamId, Uuid RoleId,
    Uuid CommandId, ulong ExpectedRevision, ActorAttribution Actor, Uuid AssignmentId, AccessGrantScope? Scope = null) : DomainEvent;
