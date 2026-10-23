using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.team-member.removed", 2)]
public sealed record TeamMemberRemoved(Uuid TenantId, Uuid TeamId, Uuid MemberId,
    Uuid MembershipEpisodeId, Uuid AssignmentEventId, Uuid? TerminationEventId = null) : DomainEvent;
