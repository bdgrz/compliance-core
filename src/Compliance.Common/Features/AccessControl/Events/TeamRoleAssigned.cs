using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.team-role.assigned", 1)]
public sealed record TeamRoleAssigned(Uuid TenantId, Uuid TeamId, Uuid RoleId,
    Uuid CommandId, ulong ExpectedRevision, ActorAttribution Actor) : DomainEvent;
