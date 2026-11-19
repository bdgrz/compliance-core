using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.primary-org-admin-designation-refused", 1)]
public sealed record PrimaryOrgAdminDesignationRefused(Uuid TenantId, Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId, string Reason) : DomainEvent;
