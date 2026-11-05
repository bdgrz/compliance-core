using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.primary-org-admin-released", 1)]
public sealed record PrimaryOrgAdminReleased(Uuid TenantId, Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId, Uuid CommandId, Uuid ReplacedByUserId, string Reason, ActorAttribution ReleasedBy) : DomainEvent;
