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
    public static async Task<AccessReviewItemState> ReadAsync(AccessReviewItem item, AccessReviewItemView frozen,
        Uuid? campaignRemediationOwnerMemberId, IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(content);
        var state = new AccessReviewItemState(frozen, campaignRemediationOwnerMemberId, item.Revision,
            item.Opened is not null, item.IsClosed,
            item.Latest is { } latest ? NativeFactReference.Of(item.Stream, latest) : null);
        foreach (var fact in item.Facts)
            switch (fact)
            {
                case AccessDecisionsRecorded decisions when !item.IsVisible(decisions):
                    // A provisional bulk decision is hidden until the batch's publication settles it on this item.
                    break;
                case AccessDecisionsRecorded decisions:
                    state.Decisions.AddRange(await AccessReviewContent.ReadAsync<IReadOnlyList<AccessDecisionView>>(
                        content, tenantId, decisions.DecisionsManifest, ct).ConfigureAwait(false));
                    break;
                case AccessRemediationChangeRecorded change:
                    state.Changes.Add(await AccessReviewContent.ReadAsync<AccessRemediationChangeView>(content,
                        tenantId, change.ChangeContent, ct).ConfigureAwait(false));
                    break;
                case AccessRemediationVerified verified:
                    state.Verification = await AccessReviewContent.ReadAsync<AccessRemediationVerificationView>(
                        content, tenantId, verified.VerificationContent, ct).ConfigureAwait(false);
                    break;
                case AccessRemediationExceptionRecorded exception:
                    state.Exception = await AccessReviewContent.ReadAsync<AccessRemediationExceptionView>(content,
                        tenantId, exception.ExceptionContent, ct).ConfigureAwait(false);
                    break;
                case AccessReviewResponsibilityReassigned reassigned:
                    state.Reassignments.Add(await AccessReviewContent
                        .ReadAsync<AccessReviewResponsibilityReassignmentView>(content, tenantId,
                            reassigned.ReassignmentContent, ct).ConfigureAwait(false));
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
        return await AccessReviewContent.ReadAsync<AccessReviewItemView>(content, tenantId, intent.PopulationEntry, ct)
            .ConfigureAwait(false);
    }
}
