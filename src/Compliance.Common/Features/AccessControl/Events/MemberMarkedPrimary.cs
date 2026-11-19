using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.member-marked-primary", 1)]
public sealed record MemberMarkedPrimary(Uuid TenantId, Uuid MemberId, Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId) : DomainEvent;
