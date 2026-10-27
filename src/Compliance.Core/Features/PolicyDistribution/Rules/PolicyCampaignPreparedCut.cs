using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Rules;

/// <summary>The precise local native authority against which immutable feasibility was prepared.</summary>
sealed record PolicyCampaignPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static PolicyCampaignPreparedCut? Capture(Aggregate owner) => owner switch
    {
        PolicyDistributionCampaign value => new(value.Stream, value.Version, value.CommittedStreamPosition, value.ReplayCut.Digest),
        PolicyCampaignParticipant value => new(value.Stream, value.Version, value.CommittedStreamPosition, value.ReplayCut.Digest),
        _ => null,
    };

    public bool Matches(Aggregate owner) => this == Capture(owner);

    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The campaign native authority changed while immutable content was prepared.", true));
}
