using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.member-primary-mark-refused", 1)]
public sealed record MemberPrimaryMarkRefused(Uuid TenantId, Uuid MemberId, Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId, string Reason) : DomainEvent;
