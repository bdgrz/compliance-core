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
    AccessReviewManifests manifests, IDomainEventReader events)
{
    public async ValueTask<AccessReviewCompletion> VerifyAsync(Uuid tenantId, Uuid campaignId, NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid selectedLogicalId, CancellationToken ct)
    {
        var native = await ReadNativeAsync(tenantId, campaignId, source, intentReference, operationId, selectedLogicalId, ct)
            .ConfigureAwait(false);
        return native.Error is not null ? native : await PrepareAsync(tenantId, native, ct).ConfigureAwait(false);
    }

    /// <summary>Verifies the actual scoped native source before immutable binding and receiver decisions.</summary>
    public async ValueTask<AccessReviewCompletion> ReadNativeAsync(Uuid tenantId, Uuid campaignId,
        NativeFactReference source, ImmutableContentRef intentReference, Uuid operationId, Uuid selectedLogicalId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId, selectedLogicalId, operationId), ct)
            .ConfigureAwait(false);
        if (campaign.AcceptedFor(operationId) is not { } accepted)
            return AccessReviewCompletion.Failed(Transient("The accepted completion is not visible yet."));
        return source.Stream != campaign.Stream || accepted.Metadata.EventId != source.EventId ||
            accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference ||
            accepted.Operation.RootOperationId != operationId
            ? AccessReviewCompletion.Failed(Permanent("The source is not the accepted completion of this campaign."))
            : new AccessReviewCompletion(campaign, accepted, null, null);
    }

    /// <summary>Binds immutable command terms absent from the native receipt, including the required use-manifest identity.</summary>
    public async ValueTask<AccessReviewCompletion> BindAsync(Uuid tenantId, AccessReviewCompletion native, CancellationToken ct)
    {
        var campaign = native.Campaign!;
        var accepted = native.Accepted!;
        try
        {
            var intent = await AccessReviewContent.ReadAsync<ClosureIntent>(content, tenantId, accepted.Intent, ct)
                .ConfigureAwait(false);
            if (intent.Header is null || intent.Header.RootOperationId != accepted.Operation.RootOperationId ||
                intent.ParentKind != ClosureParentKind.AccessReviewCampaign || intent.ParentId != campaign.Id ||
                intent.ParentGeneration != (ulong)AccessReviewEffects.Generation || intent.SourceUses is null)
                return AccessReviewCompletion.Failed(Permanent("The command differs from the frozen completion intent."));
            AccessReviewIntentBindings.Require(intent.Header, accepted.Operation);
            return native with { Intent = intent };
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return AccessReviewCompletion.Failed(new RequestError(RequestErrorKind.Conflict, error.Message,
                error is ImmutableContentUnavailableException));
        }
    }

    /// <summary>Verifies the complete frozen selection only for a new receiver's feasibility.</summary>
    public async ValueTask<AccessReviewCompletion> PrepareAsync(Uuid tenantId, AccessReviewCompletion native, CancellationToken ct)
    {
        var bound = native.Intent is null ? await BindAsync(tenantId, native, ct).ConfigureAwait(false) : native;
        if (bound.Error is not null)
            return bound;
        try
        {
            await RequireCompleteSelectionAsync(tenantId, bound.Campaign!, bound.Accepted!, bound.Intent!, ct).ConfigureAwait(false);
            return bound;
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return AccessReviewCompletion.Failed(new RequestError(RequestErrorKind.Conflict, error.Message,
                error is ImmutableContentUnavailableException));
        }
    }

    async Task RequireCompleteSelectionAsync(Uuid tenant, AccessReviewCampaign campaign,
        AccessReviewCampaignCompletionAccepted accepted, ClosureIntent intent, CancellationToken ct)
    {
        var realm = AccessReviewContent.Realm(tenant);
        var launched = campaign.Launched ?? throw new ImmutableContentIntegrityException("A completion has no native launch.");
        var items = await manifests.ItemsAsync(realm, launched.ItemsManifest, ct).ConfigureAwait(false);
        var requirements = await manifests.RequirementsAsync(realm, intent.SourceUses, ct).ConfigureAwait(false);
        if (requirements.Count != items.Count || items.Values.Any(item =>
            !requirements.TryGetValue(AccessReviewEffects.ItemStream(tenant, campaign.Id, item.ItemId).Resource, out var use) ||
            use.SourceOwner != AccessReviewEffects.ItemStream(tenant, campaign.Id, item.ItemId) ||
            use.ContentEvent.Stream != use.SourceOwner || use.ContentEvent.EventId == Uuid.Empty ||
            use.ContentEvent.AggregateVersion == 0 || use.ResourceRevision == 0 || use.Purpose != SourceUsePurpose.CampaignCompletion))
            throw new ImmutableContentIntegrityException("The closure does not pin every exact launched item once.");
        var history = await AccessReviewCampaignHistory.ReadAsync(events, campaign, ct).ConfigureAwait(false);
        var expected = new Dictionary<(Uuid Batch, Uuid Item), NativeFactReference>();
        foreach (var admission in history.AdmittedBatches.Where(fact => fact.Metadata.AggregateVersion < accepted.Metadata.AggregateVersion))
        {
            var bulk = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenant, admission.Intent, ct)
                .ConfigureAwait(false);
            AccessReviewIntentBindings.Require(bulk.Header, admission.Operation);
            if (bulk.CampaignId != campaign.Id || bulk.DecisionBatchId != admission.Operation.RootOperationId ||
                bulk.CampaignGeneration != (ulong)AccessReviewEffects.Generation)
                throw new ImmutableContentIntegrityException("A drained bulk admission belongs to another exact campaign.");
            foreach (var item in (await manifests.BulkItemsAsync(realm, bulk.Items, ct).ConfigureAwait(false)).Values)
            {
                if (!items.ContainsKey(item.ItemId) || item.ItemOwner != AccessReviewEffects.ItemStream(tenant, campaign.Id, item.ItemId) ||
                    !expected.TryAdd((bulk.DecisionBatchId, item.ItemId), NativeFactReference.Of(campaign.Stream, admission)))
                    throw new ImmutableContentIntegrityException("The admitted drain repeats or substitutes a selected item.");
            }
        }
        var drains = await manifests.DrainsAsync(realm, intent.AdmittedChildren, ct).ConfigureAwait(false);
        var actual = drains.Values.SelectMany(static selections => selections).ToArray();
        if (actual.Length != expected.Count || actual.Any(value =>
            !expected.TryGetValue((value.DecisionBatchId, value.ItemId), out var source) || source != value.Admission))
            throw new ImmutableContentIntegrityException("The closure does not freeze the complete exact set of earlier admitted bulk item selections.");
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
