using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The folded state of one frozen item: its decisions, provider changes, verification, exception and
///     reassignments read from the verified content of its facts. It answers the read questions the campaign
///     aggregate answered before the split. An item the launch fan-out has not materialized yet has revision zero and
///     no history.
/// </summary>
public sealed class AccessReviewItemState(AccessReviewItemView item, Uuid? campaignRemediationOwnerMemberId,
    long revision, bool materialized, bool closed, NativeFactReference? latest)
{
    /// <summary>The frozen item.</summary>
    public AccessReviewItemView Item { get; } = item;

    /// <summary>The item's own logical revision.</summary>
    public long Revision { get; } = revision;

    /// <summary>True once the launch fan-out materialized the item.</summary>
    public bool Materialized { get; } = materialized;

    /// <summary>True once a completed campaign closed the item.</summary>
    public bool Closed { get; } = closed;

    /// <summary>The latest business fact of the item, the head a campaign completion pins.</summary>
    public NativeFactReference? Latest { get; } = latest;

    /// <summary>Every decision in order.</summary>
    public List<AccessDecisionView> Decisions { get; } = [];

    /// <summary>Every provider change in order.</summary>
    public List<AccessRemediationChangeView> Changes { get; } = [];

    /// <summary>Every responsibility reassignment in order.</summary>
    public List<AccessReviewResponsibilityReassignmentView> Reassignments { get; } = [];

    /// <summary>The latest verification.</summary>
    public AccessRemediationVerificationView? Verification { get; set; }

    /// <summary>The latest exception.</summary>
    public AccessRemediationExceptionView? Exception { get; set; }

    /// <summary>The current decision.</summary>
    public AccessDecisionView? CurrentDecision => Decisions.Count == 0 ? null : Decisions[^1];

    /// <summary>The current reviewer: the latest reassignment, else the launch-frozen reviewer.</summary>
    public Uuid CurrentReviewerMemberId => Reassignments.LastOrDefault(static reassignment =>
        reassignment.Responsibility == AccessReviewResponsibilityKind.Reviewer)?.AssignedMemberId ?? Item.ReviewerMemberId;

    /// <summary>The current remediation owner: the latest reassignment, else the campaign-frozen owner.</summary>
    public Uuid? CurrentRemediationOwnerMemberId => Reassignments.LastOrDefault(static reassignment =>
        reassignment.Responsibility == AccessReviewResponsibilityKind.RemediationOwner)?.AssignedMemberId ??
        campaignRemediationOwnerMemberId;

    /// <summary>The remediation status as of <paramref name="now" />.</summary>
    public string RemediationStatus(DateTimeOffset now)
    {
        if (!AccessReviewVocabulary.RequiresRemediation(CurrentDecision?.Decision))
            return "not_required";
        if (Verification is not null)
            return "verified";
        if (Exception is { } exception && (exception.ExpiresAt is null || now < exception.ExpiresAt))
            return "excepted";
        return Changes.Count > 0 ? "provider_changed" : "pending";
    }

    /// <summary>Explains why the reviewer cannot decide the item, or null when they can.</summary>
    public string? Eligibility(Uuid reviewerMemberId, Uuid reviewerUserId, bool bulk)
    {
        if (CurrentReviewerMemberId != reviewerMemberId)
            return "not_assigned";
        if (Item.SubjectUserId == reviewerUserId)
            return "self_review";
        return bulk && Item.Privileged ? "privileged_requires_individual_decision" : null;
    }

    /// <summary>Explains why the item blocks completion at <paramref name="now" />, or null when it does not.</summary>
    public string? CompletionBlocker(DateTimeOffset now)
    {
        if (!Materialized || Decisions.Count == 0)
            return $"Item {Item.ItemId} is unresolved.";
        return RemediationStatus(now) is "pending" or "provider_changed"
            ? $"Item {Item.ItemId} requires verified remediation or an approved exception."
            : null;
    }

    /// <summary>The public view of the item as of <paramref name="now" />.</summary>
    public AccessReviewItemStateView ToView(DateTimeOffset now) => new(Item,
        Decisions.Count == 0 ? "unresolved" : "decided", CurrentDecision, [.. Decisions], RemediationStatus(now),
        [.. Changes], Verification, Exception, CurrentReviewerMemberId, CurrentRemediationOwnerMemberId,
        [.. Reassignments], Revision);
}
