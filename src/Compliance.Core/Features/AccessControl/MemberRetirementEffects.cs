using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class MemberRetirementEffects
{
    public static Uuid CompletionId(Uuid sourceEventId, Uuid memberId, Uuid episodeId) =>
        Uuid.CreateVersion5(sourceEventId, $"member-retirement:complete:{memberId}:{episodeId}");

    public static Uuid RelationshipId(Uuid sourceEventId, EventStreamAddress owner, Uuid episodeId, ulong position,
        Uuid terminationEventId) =>
        Uuid.CreateVersion5(sourceEventId, FormattableString.Invariant(
            $"member-retirement:relationship:{owner}:{episodeId}:{position}:{terminationEventId}"));

    public static CompleteMemberRetirement Complete(MembershipTerminationProof proof) => new(
        proof.TenantId, proof.MemberId, proof.MembershipEpisodeId, proof.EventId,
        CompletionId(proof.EventId, proof.MemberId, proof.MembershipEpisodeId));

    public static EventStreamAddress RelationshipStream(CleanupRetiredMembershipRelationship request) =>
        request.Kind == RetiredMembershipRelationshipKind.TeamAssignment
            ? new TeamMember(request.TenantId, request.TargetId, request.MemberId).Stream
            : new AccessGrant(request.TenantId, request.TargetId).Stream;

    public static CleanupRetiredMembershipRelationship Team(TeamMemberAssigned source, ulong position,
        Uuid terminationEventId) =>
        new(source.TenantId, source.MemberId, source.MembershipEpisodeId,
            RetiredMembershipRelationshipKind.TeamAssignment, source.TeamId, source.Metadata.EventId, position, terminationEventId,
            RelationshipId(source.Metadata.EventId, new TeamMember(source.TenantId, source.TeamId,
                source.MemberId).Stream, source.MembershipEpisodeId, position, terminationEventId));

    public static CleanupRetiredMembershipRelationship Grant(AccessGrantIssued source, ulong position,
        Uuid terminationEventId) =>
        new(source.TenantId, source.Terms.Principal.Id, source.MembershipEpisodeId ?? Uuid.Empty,
            RetiredMembershipRelationshipKind.DirectGrant, source.GrantId, source.Metadata.EventId, position, terminationEventId,
            RelationshipId(source.Metadata.EventId, new AccessGrant(source.TenantId, source.GrantId).Stream,
                source.MembershipEpisodeId ?? Uuid.Empty, position, terminationEventId));
}
