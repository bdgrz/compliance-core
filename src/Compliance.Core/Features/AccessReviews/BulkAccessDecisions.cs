using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Computes a bulk decision preview. Its token binds the campaign revision, the reviewer, the
///     decision, and the exact eligible items at their own revisions, so a changed campaign or item
///     invalidates the preview.
/// </summary>
public static class BulkAccessDecisions
{
    public static Result<BulkAccessDecisionPreview> Preview(AccessReviewCampaignSnapshot campaign,
        IReadOnlyList<Uuid> itemIds, string decision, Uuid reviewerMemberId, Uuid reviewerUserId)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (!campaign.IsLaunched || !campaign.IsReviewer(reviewerMemberId))
            return AccessReviewOutcome.Failure<BulkAccessDecisionPreview>(RequestErrorKind.NotFound,
                "The campaign was not found.");
        if (itemIds is not { Count: > 0 } || itemIds.Count > 500 ||
            decision is null || !AccessReviewVocabulary.Decisions.Contains(decision))
            return AccessReviewOutcome.Failure<BulkAccessDecisionPreview>(RequestErrorKind.Validation,
                "A bulk preview names between 1 and 500 items and a valid decision.");
        var eligible = new List<AccessReviewItemView>();
        var rejected = new List<BulkAccessDecisionRejection>();
        foreach (var itemId in itemIds.Distinct())
        {
            var reason = campaign.Eligibility(itemId, reviewerMemberId, reviewerUserId, bulk: true);
            if (reason is null)
                eligible.Add(campaign.FindItem(itemId)!);
            else
                rejected.Add(new BulkAccessDecisionRejection(itemId,
                    reason == "not_assigned" ? "not_found" : reason));
        }
        return Result<BulkAccessDecisionPreview>.Success(new BulkAccessDecisionPreview(campaign.Id,
            campaign.Revision, decision, eligible, rejected,
            Token(campaign.Id, campaign.Revision, reviewerMemberId, decision,
                eligible.Select(item => $"{item.ItemId}@{campaign.State(item.ItemId)!.Revision}"))));
    }

    static string Token(Uuid campaignId, long revision, Uuid reviewerMemberId, string decision,
        IEnumerable<string> itemHeads)
    {
        var material = string.Join('\n', new[]
        {
            "bdgrz.access_review.bulk_decision.v1", campaignId.ToString(),
            revision.ToString(CultureInfo.InvariantCulture), reviewerMemberId.ToString(), decision,
        }.Concat(itemHeads.Order(StringComparer.Ordinal)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
