using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Deterministic team assignment and removal commands dispatched by internal reactors, so replays reuse one
///     logical id. System callers bind to the member's current episode and the owner's current state by sending an
///     empty episode and negative expectations; a caller that registered the member itself passes the episode it
///     registered so a replay can never assign a later episode.
/// </summary>
static class TeamMemberEffects
{
    /// <summary>The assignment command a source event causes for one member on one team.</summary>
    public static AssignTeamMember Assign(Uuid sourceEventId, Uuid tenantId, Uuid teamId, Uuid memberId,
        Uuid memberEpisodeId = default) =>
        new(CommandId(sourceEventId, "team-member:assign", tenantId, teamId, memberId), tenantId, teamId, memberId,
            memberEpisodeId, -1, -1);

    /// <summary>
    ///     The removal a source event causes for one proven assignment. The command id names the assignment episode, and
    ///     the command carries the exact membership episode, revision and generation the source facts proved, so the
    ///     removal can close that assignment and never a newer one.
    /// </summary>
    public static RemoveTeamMember Remove(Uuid sourceEventId, Uuid tenantId, Uuid teamId, TeamAssignmentSource source) =>
        new(EffectIdentity.CommandId(sourceEventId, "team-member:remove", $"{tenantId}:{teamId}:{source.MemberId}",
                source.AssignmentEventId.ToString()), tenantId, teamId, source.MemberId, source.MembershipEpisodeId,
            checked((long)source.Revision), checked((long)source.Generation));

    static Uuid CommandId(Uuid sourceEventId, string effectKind, Uuid tenantId, Uuid teamId, Uuid memberId) =>
        EffectIdentity.CommandId(sourceEventId, effectKind, $"{tenantId}:{teamId}:{memberId}", null);
}
