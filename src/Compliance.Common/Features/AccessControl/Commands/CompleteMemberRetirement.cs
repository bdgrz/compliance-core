using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Recovers cleanup of the exact episode identified by a committed retirement fact.</summary>
[Discriminator("bdgrz.rbac.member.retirement.complete", 1)]
public sealed record CompleteMemberRetirement(Uuid TenantId, Uuid MemberId, Uuid MembershipEpisodeId,
    Uuid TerminationEventId, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
