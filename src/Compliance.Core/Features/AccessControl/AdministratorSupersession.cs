using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Decides, from the target owner's own history, whether an apply step is not there yet, proven superseded by a
///     later episode, or free to proceed. Only a proven later episode or closed assignment supersedes; absence of the
///     dependency is a transient wait.
/// </summary>
static class AdministratorSupersession
{
    /// <summary>The wait or supersession a member target implies for the episode, or null to proceed.</summary>
    public static Result<ReceiverOutcome>? Of(Member member, Uuid episode)
    {
        if (member.Version == 0)
            return AdministratorReceiverSupport.Wait("The member is not available yet.");
        if (member.MembershipEpisodeId != episode)
            return AdministratorReceiverSupport.Superseded("A later membership episode replaced this one.");
        return member.IsRegistered
            ? null
            : AdministratorReceiverSupport.Superseded("The membership episode already ended.");
    }

    /// <summary>The wait or supersession an assignment target implies for the episode, or null to proceed.</summary>
    public static Result<ReceiverOutcome>? Of(TeamMember assignment, Uuid episode)
    {
        if (assignment.Version == 0)
            return AdministratorReceiverSupport.Wait("The administrators-team assignment is not available yet.");
        if (!assignment.IsAssigned)
            return AdministratorReceiverSupport.Superseded("The administrators-team assignment already closed.");
        return assignment.MembershipEpisodeId == episode
            ? null
            : AdministratorReceiverSupport.Superseded("A later membership episode replaced this assignment.");
    }

    /// <summary>The wait or supersession the guard's ordering implies for a restoration, or null to proceed.</summary>
    public static Result<ReceiverOutcome>? Of(GateDecision gate) => gate switch
    {
        GateDecision.Wait => AdministratorReceiverSupport.Wait("An earlier withdrawal of this episode is unfinished."),
        GateDecision.Superseded => AdministratorReceiverSupport.Superseded("A later withdrawal replaced this restoration."),
        _ => null,
    };
}
