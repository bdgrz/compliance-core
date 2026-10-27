using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Recovers cleanup of the exact episode identified by a committed retirement fact.</summary>
[Discriminator("bdgrz.rbac.member.retirement.complete", 1)]
public sealed record CompleteMemberRetirement(Uuid TenantId, Uuid MemberId, Uuid MembershipEpisodeId,
    Uuid TerminationEventId, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
