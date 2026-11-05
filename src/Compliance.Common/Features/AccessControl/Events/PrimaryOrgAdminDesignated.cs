using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.primary-org-admin-designated", 1)]
public sealed record PrimaryOrgAdminDesignated(Uuid TenantId, Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId) : DomainEvent;
