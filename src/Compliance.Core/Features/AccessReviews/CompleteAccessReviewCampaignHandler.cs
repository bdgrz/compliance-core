using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Admits the owner's personal completion sign-off on the campaign against the campaign's own revision and
///     returns the fixed 202 acknowledgment; HTTP-only. It freezes the closure intent: the authored attestation, the
///     complete drained item selection and the exact pinned head of every item. The completion process then holds
///     every item, freezes the final campaign snapshot and closes the campaign. An exact retry replays the original
///     acknowledgment even after the campaign completed.
/// </summary>
public sealed class CompleteAccessReviewCampaignHandler(NativeOperationExecutor operations, AccessReviewReader campaigns,
    IImmutableContentStore content, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<CompleteAccessReviewCampaign, OperationAcceptedResponse>
{
    const string CommandKind = nameof(CompleteAccessReviewCampaign);

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<CompleteAccessReviewCampaign> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact campaign source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(
        IRequestContext<CompleteAccessReviewCampaign> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A completion requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var campaign = await campaigns.ReadCampaignIdentityAsync(request.TenantId, request.CampaignId, request.CommandId, ct)
            .ConfigureAwait(false);
        if (!campaign.IsLaunched ||
            !await RestrictedAccessReviewVisibility.CanReadCampaignItemsAsync(visibility, request.TenantId,
                actor.UserId, campaign, ct: ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound, "The campaign was not found.");

        var now = clock.GetUtcNow();
        var stream = campaign.Campaign.Stream;
        var provenance = AccessReviewEffects.Root(request.CommandId, CommandKind, stream, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? intent = null;
        AccessReviewPreparedCut? preparedCut = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessReviewCampaign>(binding,
            () => new AccessReviewCampaign(request.TenantId, request.CampaignId, request.CommandId),
            owner => owner.Acceptance,
            async (owner, prepareCt) =>
            {
                var prepared = owner.PrepareCompletion(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var preparedCampaign = await campaigns.ReadCampaignAsync(request.TenantId, owner, prepareCt)
                    .ConfigureAwait(false);
                if (preparedCampaign.CompletionBlockers(now) is { Count: > 0 } blockers)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict, string.Join(" ", blockers.Take(10)));
                if (!AccessReviewVocabulary.IsBoundedText(request.Attestation, 4000))
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "Completion requires an attestation of at most 4000 characters.");
                intent = await FreezeIntentAsync(request, preparedCampaign, provenance, now, prepareCt).ConfigureAwait(false);
                response = await AccessReviewContent.AcceptedResponseAsync(content, request.TenantId, request.CommandId,
                    now, prepareCt).ConfigureAwait(false);

                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AccessReviewCampaign owner, ImmutableContentRef acknowledgment) =>
            {
                if (preparedCut?.Matches(owner) != true)
                    return AccessReviewPreparedCut.Changed();
                return owner.AcceptCompletion(provenance, intent!, acknowledgment);
            }), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.AcknowledgmentAsync(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async Task<ImmutableContentRef> FreezeIntentAsync(CompleteAccessReviewCampaign request,
        AccessReviewCampaignSnapshot campaign, OperationProvenance provenance, DateTimeOffset now, CancellationToken ct)
    {
        var tenantId = request.TenantId;
        var requirements = new SortedDictionary<string, SourceUseRequirement>(StreamKeyOrder.Instance);
        foreach (var state in campaign.Items)
        {
            var stream = AccessReviewEffects.ItemStream(tenantId, request.CampaignId, state.Item.ItemId);
            var decision = state.CurrentDecision!;
            var facts = await AccessReviewContent.WriteAsync(content, tenantId, AccessReviewContent.ItemFactsSchema,
                new AccessReviewItemCompletionFacts(state.Item.ItemId, state.Revision, decision.DecisionId,
                    decision.Decision, state.RemediationStatus(now)), ct).ConfigureAwait(false);
            requirements.Add(stream.Resource, new SourceUseRequirement(stream, state.Latest!, (ulong)state.Revision,
                SourceUsePurpose.CampaignCompletion, facts));
        }

        var uses = await ManifestAsync(tenantId, AccessReviewContent.SourceUseItemSchema, [.. requirements.Values], ct)
            .ConfigureAwait(false);
        var admitted = await DrainedBatchesAsync(tenantId, campaign.Campaign, campaign.History, ct).ConfigureAwait(false);
        var decisionContent = await AccessReviewContent.WriteAsync(content, tenantId,
            AccessReviewContent.CompletionDecisionSchema, new AccessReviewCompletionDecision(request.CommandId,
                request.CampaignId, request.ExpectedRevision, request.Attestation.Trim()), ct).ConfigureAwait(false);
        var header = new DurableIntentHeader(request.CommandId, provenance.Owner, provenance.Owner, provenance.Owner,
            provenance.Actor, provenance.Episode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256,
            now);
        return await AccessReviewContent.WriteAsync(content, tenantId, ClosureIntent.Schema,
            new ClosureIntent(header, ClosureParentKind.AccessReviewCampaign, request.CampaignId,
                (ulong)AccessReviewEffects.Generation, admitted, decisionContent, uses, null), ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     The complete set of bulk decision batches admitted before this seal, one entry per selected item. The
    ///     completion holds an item only after every batch that selects it settled, so these drain first.
    /// </summary>
    async Task<ImmutableSelectionManifest> DrainedBatchesAsync(Uuid tenantId, AccessReviewCampaign campaign,
        AccessReviewCampaignHistorySnapshot history, CancellationToken ct)
    {
        var selections = new SortedDictionary<string, AccessReviewAdmittedBatchSelection>(StreamKeyOrder.Instance);
        foreach (var admitted in history.AdmittedBatches)
        {
            var intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenantId,
                admitted.Intent, ct).ConfigureAwait(false);
            var items = await ImmutableSelectionManifests.ReadAsync<BulkAccessDecisionItemIntent>(content,
                AccessReviewContent.Realm(tenantId), intent.Items, ct).ConfigureAwait(false);
            foreach (var item in items)
                selections.Add($"{item.ItemOwner.Resource}|{intent.DecisionBatchId}",
                    new AccessReviewAdmittedBatchSelection(intent.DecisionBatchId, item.ItemId,
                        NativeFactReference.Of(campaign.Stream, admitted)));
        }

        return await ManifestAsync(tenantId, AccessReviewContent.AdmittedChildrenSchema, [.. selections.Values], ct)
            .ConfigureAwait(false);
    }

    async Task<ImmutableSelectionManifest> ManifestAsync<T>(Uuid tenantId, string itemSchema, T[] items,
        CancellationToken ct) where T : notnull
    {
        var reference = await AccessReviewContent.WriteAsync<IReadOnlyList<T>>(content, tenantId,
            itemSchema, items, ct).ConfigureAwait(false);
        return new ImmutableSelectionManifest(reference, itemSchema, items.Length, SelectionOrder.CanonicalKey,
            reference.Sha256);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<OperationAcceptedResponse>(kind, message);
}
