using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Deterministic team assignment and removal commands dispatched by internal reactors, so replays reuse one
///     logical id. System callers bind to the member's current episode and the owner's current state by sending an
///     empty episode and negative expectations.
/// </summary>
static class TeamMemberEffects
{
    /// <summary>The assignment command a source event causes for one member on one team.</summary>
    public static AssignTeamMember Assign(Uuid sourceEventId, Uuid tenantId, Uuid teamId, Uuid memberId) =>
        new(CommandId(sourceEventId, "team-member:assign", tenantId, teamId, memberId), tenantId, teamId, memberId,
            Uuid.Empty, -1, -1);

    /// <summary>The removal command a source event causes for one member on one team.</summary>
    public static RemoveTeamMember Remove(Uuid sourceEventId, Uuid tenantId, Uuid teamId, Uuid memberId) =>
        new(CommandId(sourceEventId, "team-member:remove", tenantId, teamId, memberId), tenantId, teamId, memberId,
            Uuid.Empty, -1, -1);

    static Uuid CommandId(Uuid sourceEventId, string effectKind, Uuid tenantId, Uuid teamId, Uuid memberId) =>
        EffectIdentity.CommandId(sourceEventId, effectKind, $"{tenantId}:{teamId}:{memberId}", null);
}
