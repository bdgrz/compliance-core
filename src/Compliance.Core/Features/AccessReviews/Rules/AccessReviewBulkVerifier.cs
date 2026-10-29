using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Rules;

/// <summary>
///     Verifies a receiver's proven source: the frozen bulk intent is read from verified immutable content and the
///     admitted batch fact is found on the authoritative campaign. The source reference only names the fact; nothing a
///     command states is trusted until the campaign confirms it.
/// </summary>
sealed class AccessReviewBulkVerifier(IAggregateReader reader, IImmutableContentStore content,
    AccessReviewManifests manifests)
{
    public async ValueTask<AccessReviewBulk> VerifyAsync(Uuid tenantId, Uuid campaignId, NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, CancellationToken ct)
    {
        var native = await ReadNativeAsync(tenantId, campaignId, source, intentReference, operationId, ct).ConfigureAwait(false);
        return native.Error is not null ? native : await PrepareAsync(tenantId, native, ct).ConfigureAwait(false);
    }

    /// <summary>Resolves exact native admission authority before immutable preparation or receiver receipt knowledge.</summary>
    public async ValueTask<AccessReviewBulk> ReadNativeAsync(Uuid tenantId, Uuid campaignId, NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId, selectedRootId: operationId), ct)
            .ConfigureAwait(false);
        if (campaign.AdmittedFor(operationId) is not { } admitted)
            return AccessReviewBulk.Failed(Transient("The admitted bulk decision is not visible yet."));
        return source.Stream != campaign.Stream || admitted.Metadata.EventId != source.EventId ||
            admitted.Metadata.AggregateVersion != source.AggregateVersion || admitted.Intent != intentReference ||
            admitted.Operation.RootOperationId != operationId
            ? AccessReviewBulk.Failed(Permanent("The source is not the admitted bulk decision of this campaign."))
            : new AccessReviewBulk(campaign, admitted, null, null);
    }

    /// <summary>Validates the complete frozen bulk selection for a new receiver candidate.</summary>
    public async ValueTask<AccessReviewBulk> PrepareAsync(Uuid tenantId, AccessReviewBulk native, CancellationToken ct)
    {
        var campaign = native.Campaign!;
        var admitted = native.Admitted!;
        BulkAccessDecisionIntent intent;
        try
        {
            intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenantId, admitted.Intent, ct)
                .ConfigureAwait(false);
            if (intent.Header is null || intent.Header.RootOperationId != admitted.Operation.RootOperationId ||
                intent.DecisionBatchId != admitted.Operation.RootOperationId || intent.CampaignId != campaign.Id ||
                intent.CampaignGeneration != (ulong)AccessReviewEffects.Generation)
                return AccessReviewBulk.Failed(Permanent("The command differs from the frozen bulk decision intent."));
            AccessReviewIntentBindings.Require(intent.Header, admitted.Operation);
            var items = await manifests.BulkItemsAsync(AccessReviewContent.Realm(tenantId), intent.Items, ct).ConfigureAwait(false);
            if (items.Count == 0 || intent.ExpectedCampaignRevision != admitted.Operation.OriginalExpectedRevision ||
                items.Values.Any(item => item.ItemId == Uuid.Empty || item.ReviewerMemberId == Uuid.Empty ||
                    item.ExpectedItemRevision == 0 || item.ItemOwner != AccessReviewEffects.ItemStream(tenantId, campaign.Id, item.ItemId)))
                throw new ImmutableContentIntegrityException("The complete bulk selection differs from its admitted scoped item owners.");
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return AccessReviewBulk.Failed(new RequestError(RequestErrorKind.Conflict, error.Message,
                error is ImmutableContentUnavailableException));
        }
        return native with { Intent = intent };
    }

    /// <summary>The frozen item intent of a selected item, or null when the batch does not select it.</summary>
    public async ValueTask<BulkAccessDecisionItemIntent?> ItemOfAsync(string realm, BulkAccessDecisionIntent intent,
        string itemResource, CancellationToken ct) => (await manifests.BulkItemsAsync(realm, intent.Items, ct)
        .ConfigureAwait(false)).GetValueOrDefault(itemResource);

    /// <summary>The exact head the preview froze for an item.</summary>
    public async ValueTask<NativeFactReference> HeadOfAsync(Uuid tenantId, BulkAccessDecisionItemIntent item,
        CancellationToken ct)
    {
        var proofs = await ImmutableSelectionManifests.ReadAsync<BulkItemEligibilityProof>(content,
            AccessReviewContent.Realm(tenantId), item.EligibilityProofs, ct).ConfigureAwait(false);
        if (item.EligibilityProofs.ItemSchema != BulkItemEligibilityProof.Schema ||
            item.EligibilityProofs.Order != SelectionOrder.CanonicalKey)
            throw new ImmutableContentIntegrityException("The eligibility proof has another schema or order contract.");
        var head = Assert(proofs).ItemHead;
        if (head.Stream != item.ItemOwner || head.EventId == Uuid.Empty || head.AggregateVersion == 0)
            throw new ImmutableContentIntegrityException("The eligibility head belongs to another exact item source.");
        return head;
    }

    static BulkItemEligibilityProof Assert(IReadOnlyList<BulkItemEligibilityProof> proofs) => proofs.Count == 1
        ? proofs[0]
        : throw new ImmutableContentIntegrityException("A selected item freezes exactly one eligibility proof.");

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}

/// <summary>A verified bulk decision source, or the error that explains why it is not provable.</summary>
sealed record AccessReviewBulk(AccessReviewCampaign? Campaign, AccessReviewDecisionBatchAdmitted? Admitted,
    BulkAccessDecisionIntent? Intent, RequestError? Error)
{
    public static AccessReviewBulk Failed(RequestError error) => new(null, null, null, error);
}
