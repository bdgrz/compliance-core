using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reconciles one exact native relationship fact against authoritative membership retirement.</summary>
[Discriminator("bdgrz.rbac.member.retired-relationship.cleanup", 1)]
public sealed record CleanupRetiredMembershipRelationship(Uuid TenantId, Uuid MemberId,
    Uuid MembershipEpisodeId, RetiredMembershipRelationshipKind Kind, Uuid TargetId,
    Uuid SourceEventId, ulong SourcePosition, Uuid TerminationEventId, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
