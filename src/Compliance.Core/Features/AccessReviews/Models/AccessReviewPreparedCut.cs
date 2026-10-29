using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
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
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>The exact native Access owner authority used to prepare one immutable result.</summary>
sealed record AccessReviewPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static AccessReviewPreparedCut Capture(Aggregate owner) => owner switch
    {
        AccessExpectation expectation => new(expectation.Stream, expectation.Version, expectation.CommittedStreamPosition,
            expectation.ReplayCut.Digest),
        AccessReviewCampaign campaign => new(campaign.Stream, campaign.Version, campaign.CommittedStreamPosition,
            campaign.ReplayCut.Digest),
        AccessReviewItem item => new(item.Stream, item.Version, item.CommittedStreamPosition, item.ReplayCut.Digest),
        _ => throw new ArgumentException("The native cut is not an Access owner.", nameof(owner)),
    };
    public bool Matches(Aggregate owner) => this == Capture(owner);
    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The access review authority changed while immutable content was prepared.", true));
}
