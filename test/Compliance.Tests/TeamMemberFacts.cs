using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

/// <summary>Builds well-formed native team assignment facts for tests that only need an assigned member.</summary>
static class TeamMemberFacts
{
    static readonly ActorReference Actor = ActorReference.ForSystemProcess("system", "system");

    /// <summary>The provenance of a team operation on <paramref name="owner" /> under a logical id.</summary>
    public static OperationProvenance Provenance(TeamMember owner, Uuid commandId, string commandKind,
        ActorReference? actor = null, string authoredIntentSha256 = "", string episode = "current") =>
        owner.Provenance(commandId, commandKind, actor ?? Actor, episode, null,
            authoredIntentSha256.Length == 0 ? new string('a', 64) : authoredIntentSha256,
            new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

    /// <summary>A frozen first response reference.</summary>
    public static ImmutableContentRef FirstResponse() => new(Uuid.CreateVersion4(), new string('b', 64), 2,
        "application/json", "first-response-descriptor.v1");

    /// <summary>Assigns the member for the episode (an empty episode stays empty) without any expectation, under a fresh logical id unless one is given.</summary>
    public static Result Assign(this TeamMember owner, Uuid membershipEpisodeId = default, Uuid commandId = default) =>
        owner.ApplyAssignment(Provenance(owner, commandId == Uuid.Empty ? Uuid.CreateVersion4() : commandId,
            "AssignTeamMember"), membershipEpisodeId == Uuid.Empty ? Uuid.CreateVersion4() : membershipEpisodeId, -1, -1);

    /// <summary>Removes the current assignment without any expectation, under a fresh logical id.</summary>
    public static Result Remove(this TeamMember owner) =>
        owner.ApplyRemoval(Provenance(owner, Uuid.CreateVersion4(), "RemoveTeamMember"), Uuid.Empty, -1, -1);

    public static TeamMemberAssigned Assigned(Uuid tenantId, Uuid teamId, Uuid memberId,
        Uuid membershipEpisodeId = default, Uuid commandId = default)
    {
        var id = commandId == Uuid.Empty ? Uuid.CreateVersion4() : commandId;
        return new TeamMemberAssigned(tenantId, teamId, memberId, membershipEpisodeId, id,
            Provenance(new TeamMember(tenantId, teamId, memberId), id, "AssignTeamMember"), null);
    }

    public static TeamMemberRemoved Removed(Uuid tenantId, Uuid teamId, Uuid memberId, Uuid membershipEpisodeId,
        Uuid assignmentEventId, Uuid? terminationEventId = null, Uuid commandId = default)
    {
        var id = commandId == Uuid.Empty ? Uuid.CreateVersion4() : commandId;
        return new TeamMemberRemoved(tenantId, teamId, memberId, membershipEpisodeId, assignmentEventId,
            terminationEventId, id, Provenance(new TeamMember(tenantId, teamId, memberId), id, "RemoveTeamMember"),
            null);
    }
}
