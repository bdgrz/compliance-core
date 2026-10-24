using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Verifies a receiver's proven source: the frozen closure intent is read from verified immutable content and the
///     accepted completion fact is found on the authoritative campaign. The source reference only names the fact;
///     nothing a command states is trusted until the campaign confirms it.
/// </summary>
sealed class AccessReviewCompletionVerifier(IAggregateReader reader, IImmutableContentStore content,
    AccessReviewManifests manifests)
{
    public async ValueTask<AccessReviewCompletion> VerifyAsync(Uuid tenantId, Uuid campaignId, NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid selectedLogicalId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        ClosureIntent intent;
        try
        {
            intent = await AccessReviewContent.ReadAsync<ClosureIntent>(content, tenantId, intentReference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return AccessReviewCompletion.Failed(Permanent($"The frozen completion cannot be verified: {exception.Message}"));
        }

        if (intent.Header.RootOperationId != operationId || intent.ParentKind != ClosureParentKind.AccessReviewCampaign ||
            intent.ParentId != campaignId || intent.ParentGeneration != (ulong)AccessReviewEffects.Generation)
            return AccessReviewCompletion.Failed(Permanent("The command differs from the frozen completion intent."));
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        if (campaign.AcceptedFor(operationId) is not { } accepted)
            return AccessReviewCompletion.Failed(Transient("The accepted completion is not visible yet."));
        if (source.Stream != campaign.Stream || accepted.Metadata!.EventId != source.EventId ||
            accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference)
            return AccessReviewCompletion.Failed(Permanent("The source is not the accepted completion of this campaign."));
        return new AccessReviewCompletion(campaign, accepted, intent, null);
    }

    /// <summary>The pinned requirement of one item of the drained set, or null when the item is not in it.</summary>
    public async ValueTask<SourceUseRequirement?> RequirementOfAsync(string realm, ClosureIntent intent, string resource,
        CancellationToken ct) => (await manifests.RequirementsAsync(realm, intent.SourceUses, ct).ConfigureAwait(false))
        .GetValueOrDefault(resource);

    /// <summary>The key of an item's completion use: one per root operation, campaign owner and item.</summary>
    public static ReservationKey UseKey(Uuid operationId, AccessReviewCampaign campaign, SourceUseRequirement requirement) =>
        new(operationId, campaign.Stream, (ulong)AccessReviewEffects.Generation, requirement.SourceOwner,
            SourceUseReservations.PurposeName(requirement.Purpose));

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}

/// <summary>A verified completion source, or the error that explains why it is not provable.</summary>
sealed record AccessReviewCompletion(AccessReviewCampaign? Campaign, AccessReviewCampaignCompletionAccepted? Accepted,
    ClosureIntent? Intent, RequestError? Error)
{
    public static AccessReviewCompletion Failed(RequestError error) => new(null, null, null, error);
}
