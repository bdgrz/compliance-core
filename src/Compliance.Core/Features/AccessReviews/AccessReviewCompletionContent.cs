using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The authored closing decision a completion freezes into its closure intent.</summary>
public sealed record AccessReviewCompletionDecision(Uuid CommandId, Uuid CampaignId, long ExpectedRevision,
    string Attestation);

/// <summary>
///     What a completion pinned about one item when it was admitted: the item's own revision and the decision and
///     remediation state that satisfied the completion rules. The use is only granted while the item is exactly there.
/// </summary>
public sealed record AccessReviewItemCompletionFacts(Uuid ItemId, long Revision, Uuid DecisionId, string Decision,
    string RemediationStatus);

/// <summary>The held proof of one item recorded by the closing fact: its pin, its grant and the facts it satisfied.</summary>
public sealed record AccessReviewItemCompletionProof(Uuid ItemId, SourceUseRequirement Requirement,
    ReservationGrant Grant, AccessReviewItemCompletionFacts Facts);

/// <summary>The authoritative permanent proof mismatch that cancels a pending completion.</summary>
public sealed record AccessReviewCompletionFailure(string Reason, Uuid? ItemId, NativeFactReference? Evidence);
