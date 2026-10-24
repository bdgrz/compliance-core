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
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A completion requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        var campaign = await campaigns.ReadCampaignAsync(request.TenantId, request.CampaignId, request.CommandId, ct)
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
        if (campaign.Campaign.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (campaign.Campaign.PrepareCompletion(request.ExpectedRevision) is { IsSuccess: false } blocked)
                return Result<OperationAcceptedResponse>.Failure(blocked.Error!);
            if (campaign.CompletionBlockers(now) is { Count: > 0 } blockers)
                return Failure(RequestErrorKind.Conflict, string.Join(" ", blockers.Take(10)));
            if (!AccessReviewVocabulary.IsBoundedText(request.Attestation, 4000))
                return Failure(RequestErrorKind.Validation,
                    "Completion requires an attestation of at most 4000 characters.");
            intent = await FreezeIntentAsync(request, campaign, provenance, now, ct).ConfigureAwait(false);
            response = await AccessReviewContent.AcceptedResponseAsync(content, request.TenantId, request.CommandId,
                now, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessReviewCampaign>(binding,
            () => new AccessReviewCampaign(request.TenantId, request.CampaignId, request.CommandId),
            owner => owner.Acceptance, owner => Prepare(owner, request.ExpectedRevision, response),
            (AccessReviewCampaign owner, ImmutableContentRef acknowledgment) => owner.AcceptCompletion(provenance,
                intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.AcknowledgmentAsync(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(AccessReviewCampaign owner, long expectedRevision,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareCompletion(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The campaign changed while the completion was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
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
        var admitted = await ManifestAsync(tenantId, AccessReviewContent.AdmittedChildrenSchema, [], ct)
            .ConfigureAwait(false);
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

    async Task<ImmutableSelectionManifest> ManifestAsync(Uuid tenantId, string itemSchema,
        SourceUseRequirement[] items, CancellationToken ct)
    {
        var reference = await AccessReviewContent.WriteAsync<IReadOnlyList<SourceUseRequirement>>(content, tenantId,
            itemSchema, items, ct).ConfigureAwait(false);
        return new ImmutableSelectionManifest(reference, itemSchema, items.Length, SelectionOrder.CanonicalKey,
            reference.Sha256);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<OperationAcceptedResponse>(kind, message);
}
