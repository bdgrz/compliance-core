using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The business rules over an item's decision and remediation content (M0-D07). They are decided from the folded
///     <see cref="AccessReviewItemState" /> by the handler and re-checked against the item's revision at append, so
///     the aggregate never reads content. Messages and kinds are those of the campaign aggregate before the split.
/// </summary>
static class AccessReviewRules
{
    /// <summary>Checks a single decision; the caller already checked closure, holds and the expected revision.</summary>
    public static RequestError? CheckDecision(AccessReviewItemState state, Uuid tenantId, Uuid campaignId,
        string? decision, string? rationale, Uuid reviewerMemberId, Uuid reviewerUserId, DateTimeOffset now,
        SeparationOfDutiesWaiver? waiver)
    {
        if (decision is null || !AccessReviewVocabulary.Decisions.Contains(decision) ||
            !AccessReviewVocabulary.IsBoundedText(rationale, 4000))
            return Validation(
                "A decision is keep, modify, revoke, or unable_to_determine with a rationale of at most 4000 characters.");
        var reason = state.Eligibility(reviewerMemberId, reviewerUserId, bulk: false);
        if (reason is "not_found" or "not_assigned")
            return new RequestError(RequestErrorKind.NotFound,
                "The review item was not found among the reviewer's assignments.");
        if (reason == "self_review")
        {
            var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.AccessReviewItem,
                state.Item.ItemId, campaignId, 1, SeparationOfDutiesActions.Review);
            return waiver is null || waiver.TenantId != tenantId || !waiver.Allows(scope, reviewerMemberId, now)
                ? new RequestError(RequestErrorKind.Forbidden,
                    "A reviewer cannot decide their own access without an active exact-scope waiver.")
                : null;
        }

        if (reason is not null)
            return Validation("Privileged access requires an individual named reviewer decision.");
        return waiver is not null
            ? new RequestError(RequestErrorKind.Forbidden,
                "A separation-of-duties waiver may only be used for a current conflict.")
            : null;
    }

    /// <summary>The decision a reviewer's command records.</summary>
    public static AccessDecisionView BuildDecision(Uuid decisionId, AccessReviewItemState state, string decision,
        string rationale, Uuid reviewerMemberId, ActorReference decidedBy, DateTimeOffset now, DateTimeOffset deadline,
        Uuid? waiverId) => new(decisionId, state.Item.ItemId, decision, rationale.Trim(), reviewerMemberId, decidedBy,
        now, null, now > deadline, waiverId);

    /// <summary>Only an item decided modify or revoke requires remediation.</summary>
    public static RequestError? CheckRemediable(AccessReviewItemState state) =>
        AccessReviewVocabulary.RequiresRemediation(state.CurrentDecision?.Decision)
            ? null
            : new RequestError(RequestErrorKind.Conflict,
                "Only an item decided modify or revoke requires remediation.");

    /// <summary>Checks a provider change.</summary>
    public static RequestError? CheckChange(AccessReviewItemState state, string? reference, string? description,
        DateTimeOffset changedAt, DateTimeOffset now)
    {
        if (CheckRemediable(state) is { } failure)
            return failure;
        return !AccessReviewVocabulary.IsBoundedText(reference, 500) ||
               !AccessReviewVocabulary.IsBoundedText(description, 4000) || changedAt > now
            ? Validation(
                "A provider change requires a reference, a description, and a change time that is not in the future.")
            : null;
    }

    /// <summary>Checks a remediation verification.</summary>
    public static RequestError? CheckVerification(AccessReviewItemState state,
        AccessRemediationVerificationView verification)
    {
        if (CheckRemediable(state) is { } failure)
            return failure;
        return verification.ObservedAt <= state.CurrentDecision!.DecidedAt
            ? new RequestError(RequestErrorKind.Conflict,
                "Verification requires a population observed after the decision.")
            : null;
    }

    /// <summary>Checks a remediation exception.</summary>
    public static RequestError? CheckException(AccessReviewItemState state, Uuid approverMemberId, string? rationale,
        DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        if (CheckRemediable(state) is { } failure)
            return failure;
        if (state.CurrentReviewerMemberId == approverMemberId)
            return new RequestError(RequestErrorKind.Forbidden,
                "The item's reviewer cannot approve an exception to its remediation.");
        return !AccessReviewVocabulary.IsBoundedText(rationale, 4000) || expiresAt <= now
            ? Validation("A remediation exception requires a rationale of at most 4000 characters and a future expiry.")
            : null;
    }

    /// <summary>Checks a responsibility reassignment against the item's current responsibilities.</summary>
    public static RequestError? CheckReassignment(AccessReviewItemState state, string? responsibility,
        Uuid assignedMemberId, string? reason, string? delegationReason, ActorReference reassignedBy,
        DateTimeOffset reassignedAt, DateTimeOffset now)
    {
        if (!AccessReviewResponsibilityKind.IsKnown(responsibility) || assignedMemberId == Uuid.Empty ||
            !AccessReviewVocabulary.IsBoundedText(reason, 2000) ||
            responsibility == AccessReviewResponsibilityKind.Reviewer &&
            !AccessReviewVocabulary.IsBoundedText(delegationReason, 2000) ||
            responsibility == AccessReviewResponsibilityKind.RemediationOwner && delegationReason is not null ||
            reassignedBy.Kind != "member" ||
            !Uuid.TryParse(reassignedBy.Id, CultureInfo.InvariantCulture, out var actorMemberId) ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(reassignedBy.Display) ||
            reassignedAt == default)
            return Validation("A reassignment requires a known responsibility, member, reason, actor, and timestamp.");
        if (responsibility == AccessReviewResponsibilityKind.Reviewer && state.Decisions.Count > 0)
            return new RequestError(RequestErrorKind.Conflict,
                "A decided review item no longer needs a reviewer reassignment.");
        if (responsibility == AccessReviewResponsibilityKind.RemediationOwner &&
            state.RemediationStatus(now) is not ("pending" or "provider_changed"))
            return new RequestError(RequestErrorKind.Conflict, "Only open remediation work can change its owner.");
        var previous = responsibility == AccessReviewResponsibilityKind.Reviewer
            ? state.CurrentReviewerMemberId
            : state.CurrentRemediationOwnerMemberId;
        var other = responsibility == AccessReviewResponsibilityKind.Reviewer
            ? state.CurrentRemediationOwnerMemberId
            : state.CurrentReviewerMemberId;
        if (previous == assignedMemberId)
            return new RequestError(RequestErrorKind.Conflict, "The responsibility is already assigned to this member.");
        return other == assignedMemberId
            ? Validation("The reviewer and remediation owner must remain distinct.")
            : null;
    }

    static RequestError Validation(string message) => new(RequestErrorKind.Validation, message);
}
