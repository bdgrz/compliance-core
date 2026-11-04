using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
namespace Bdgrz.Compliance.Features.Remediation.Rules;

/// <summary>The proposal identity, immutable evidence and exact reviewer exception owned by a finding.</summary>
static class FindingClosureProposalRules
{
    public static Uuid MemberId(ActorReference? actor) => actor is { Kind: "member" } &&
        Uuid.TryParse(actor.Id, out var member) ? member : Uuid.Empty;

    public static bool ValidTerms(FindingClosureTerms? terms) => terms is not null &&
        FindingRules.IsBoundedText(terms.VerificationRationale, FindingRules.MaximumTextLength) &&
        FindingRules.IsBoundedText(terms.Rationale, FindingRules.MaximumTextLength) &&
        terms.ResolutionEvidence is { Count: >= 1 and <= 50 } evidence &&
        evidence.All(static item => item is not null && item.ArtifactId != Uuid.Empty && Digest(item.ContentSha256)) &&
        evidence.Distinct().Count() == evidence.Count;

    public static bool ValidActions(ImmutableSelectionManifest? actions, long admittedCount) => actions is not null &&
        actions.Count == admittedCount && actions.ItemSchema == FindingContent.AdmittedChildSchema &&
        actions.Order == SelectionOrder.CanonicalKey && actions.Content is { } content && content.ContentId != Uuid.Empty &&
        Digest(content.Sha256) && Digest(actions.ItemsSha256);

    public static bool CanReview(Uuid tenantId, Uuid findingId, FindingClosureProposed proposal,
        HashSet<Uuid> authors, FindingClosureReviewBinding review, bool approve)
    {
        var reviewer = MemberId(review.ReviewedBy);
        if (review.ClosureId != proposal.ClosureId || review.ProposalRevision != proposal.ProposalRevision ||
            reviewer == Uuid.Empty || review.ReviewerMemberId != reviewer || review.ReviewedAt == default)
            return false;
        if (review.Waiver is not { } waiver)
            return !authors.Contains(reviewer);
        var expected = new SeparationOfDutiesWaiverProofScope(tenantId, waiver.WaiverId, reviewer, reviewer,
            approve ? SeparationOfDutiesActions.Approve : SeparationOfDutiesActions.Review,
            SeparationOfDutiesRecordTypes.Finding, findingId, proposal.ClosureId, proposal.ProposalRevision);
        return waiver.WaiverId != Uuid.Empty && waiver.ToScope() == expected &&
            waiver.ValidFrom != default && waiver.ValidFrom <= review.ReviewedAt && review.ReviewedAt < waiver.ValidUntil &&
            waiver.ApprovalFact is { EventId: var eventId, AggregateVersion: > 0 } fact && eventId != Uuid.Empty &&
            fact.Source == new EventStreamAddress(tenantId.ToString(), "separation-of-duties-waivers", waiver.WaiverId.ToString()) &&
            Digest(fact.PayloadSha256);
    }

    static bool Digest(string? value) => value is { Length: 64 } &&
        value.All(static ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');
}
