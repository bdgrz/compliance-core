using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.primary-org-admin-designation-requested", 1)]
public sealed record PrimaryOrgAdminDesignationRequested(Uuid TenantId, Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId, ActorAttribution RequestedBy) : DomainEvent;
