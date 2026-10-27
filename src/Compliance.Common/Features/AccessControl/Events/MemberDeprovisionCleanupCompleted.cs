using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.rbac.member.deprovision_cleanup_completed", 2)]
public sealed record MemberDeprovisionCleanupCompleted(Uuid TenantId, Uuid MemberId,
    Uuid MembershipEpisodeId, Uuid TerminationEventId, Uuid CommandId, DateTimeOffset CompletedAt) : DomainEvent;
