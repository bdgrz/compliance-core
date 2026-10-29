using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.role-permission.removed", 1)]
public sealed record RolePermissionRemoved(Uuid TenantId, Uuid RoleId, string Permission,
    Uuid CommandId, ulong ExpectedRevision, ActorAttribution Actor, Uuid AssignmentId) : DomainEvent;
