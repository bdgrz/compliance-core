using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Folds the facts of a hydrated <see cref="AccessReviewItem" /> into its <see cref="AccessReviewItemState" />,
///     reading each decision, change, verification, exception and reassignment from its verified immutable content.
/// </summary>
static class AccessReviewItemFold
{
    public static async Task<AccessReviewItemState> ReadAsync(AccessReviewItem item, AccessReviewItemHistorySnapshot history, AccessReviewItemView frozen,
        Uuid? campaignRemediationOwnerMemberId, IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(content);
        if (!history.Matches(item))
            throw Invalid("The business history belongs to another exact hydrated item cut.");
        var state = new AccessReviewItemState(frozen, campaignRemediationOwnerMemberId, item.Revision,
            item.Opened is not null, item.IsClosed,
            item.Latest is { } latest ? NativeFactReference.Of(item.Stream, latest) : null);
        foreach (var fact in history.BusinessFacts)
            switch (fact)
            {
                case AccessDecisionsRecorded decisions when !history.IsVisible(decisions):
                    // A provisional bulk decision is hidden until the batch's publication settles it on this item.
                    break;
                case AccessDecisionsRecorded decisions:
                    var views = await AccessReviewContent.ReadAsync<IReadOnlyList<AccessDecisionView>>(
                        content, tenantId, decisions.DecisionsManifest, ct).ConfigureAwait(false);
                    var decisionId = decisions.DecisionBatchId is { } batch
                        ? Uuid.CreateVersion5(batch, $"bulk-decision:{item.ItemId}") : decisions.Operation.LogicalId;
                    if (views.Count != 1 || views[0].DecisionId != decisionId || views[0].ItemId != item.ItemId ||
                        views[0].DecidedBy != decisions.Operation.Actor || decisions.DecisionBatchId is null &&
                        views[0].DecidedAt != decisions.Operation.AcceptedAt)
                        throw Invalid("The immutable decision differs from its exact native item operation.");
                    state.Decisions.Add(views[0]);
                    break;
                case AccessRemediationChangeRecorded change:
                    var changed = await AccessReviewContent.ReadAsync<AccessRemediationChangeView>(content,
                        tenantId, change.ChangeContent, ct).ConfigureAwait(false);
                    if (changed.ChangeId != change.Operation.LogicalId || changed.RecordedBy != change.Operation.Actor ||
                        changed.RecordedAt != change.Operation.AcceptedAt)
                        throw Invalid("The immutable remediation change differs from its native operation.");
                    state.Changes.Add(changed);
                    break;
                case AccessRemediationVerified verified:
                    var verification = await AccessReviewContent.ReadAsync<AccessRemediationVerificationView>(
                        content, tenantId, verified.VerificationContent, ct).ConfigureAwait(false);
                    if (verification.VerifiedBy != verified.Operation.Actor || verification.VerifiedAt != verified.Operation.AcceptedAt)
                        throw Invalid("The immutable remediation verification differs from its native operation.");
                    state.Verification = verification;
                    break;
                case AccessRemediationExceptionRecorded exception:
                    var approved = await AccessReviewContent.ReadAsync<AccessRemediationExceptionView>(content,
                        tenantId, exception.ExceptionContent, ct).ConfigureAwait(false);
                    if (approved.ExceptionId != exception.Operation.LogicalId || approved.ApprovedBy != exception.Operation.Actor ||
                        approved.ApprovedAt != exception.Operation.AcceptedAt)
                        throw Invalid("The immutable remediation exception differs from its native operation.");
                    state.Exception = approved;
                    break;
                case AccessReviewResponsibilityReassigned reassigned:
                    var assignment = await AccessReviewContent.ReadAsync<AccessReviewResponsibilityReassignmentView>(content,
                        tenantId, reassigned.ReassignmentContent, ct).ConfigureAwait(false);
                    if (assignment.ReassignmentId != reassigned.Operation.LogicalId || assignment.ItemId != item.ItemId ||
                        assignment.ReassignedBy != reassigned.Operation.Actor || assignment.ReassignedAt != reassigned.Operation.AcceptedAt)
                        throw Invalid("The immutable responsibility reassignment differs from its native item operation.");
                    state.Reassignments.Add(assignment);
                    break;
            }

        return state;
    }

    /// <summary>The frozen item view of a materialized item, read from the intent it opened with.</summary>
    public static async Task<AccessReviewItemView> FrozenItemAsync(AccessReviewItem item,
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        var intent = await AccessReviewContent.ReadAsync<AccessReviewItemCreationIntent>(content, tenantId,
            item.Opened!.Intent, ct).ConfigureAwait(false);
        var frozen = await AccessReviewContent.ReadAsync<AccessReviewItemView>(content, tenantId, intent.PopulationEntry, ct)
            .ConfigureAwait(false);
        if (intent.CampaignId != item.CampaignId || intent.ItemId != item.ItemId ||
            intent.SystemInstanceId != frozen.SystemInstanceId || intent.ItemId != frozen.ItemId ||
            intent.ReviewerMemberId != frozen.ReviewerMemberId || intent.CampaignGeneration != (ulong)AccessReviewEffects.Generation ||
            intent.Header.FirstOwner != AccessReviewEffects.CampaignStream(tenantId, item.CampaignId) ||
            intent.Header.RootOperationId != item.CampaignId ||
            item.Opened.Operation.RootOperationId != intent.Header.RootOperationId ||
            item.Opened.Operation.FirstOwner != intent.Header.FirstOwner ||
            item.Opened.Operation.AuthoredIntentSha256 != intent.Header.AuthoredIntentSha256)
            throw new ImmutableContentIntegrityException("The materialized item body differs from its exact frozen launch scope.");
        return frozen;
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
