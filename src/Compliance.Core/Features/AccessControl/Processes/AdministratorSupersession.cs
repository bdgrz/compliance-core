using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Processes;

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
