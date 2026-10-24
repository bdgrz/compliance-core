using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Admits the reviewer's complete bulk decision on the campaign against the campaign's own revision and returns the
///     fixed 202 acknowledgment; HTTP-only. It admits only the exact previewed selection: the request must name
///     every eligible item of the preview and present its token, and the frozen intent keeps each selected item's own
///     head (its revision and event) so a changed item conflicts before any decision can publish. The independent
///     <see cref="AccessReviewDecisionBatch" /> is created from the admission at its own revision zero; nothing here
///     compares the campaign revision with it. An exact retry replays the original acknowledgment even after the batch
///     published or the campaign completed.
/// </summary>
public sealed class RecordBulkAccessDecisionHandler(NativeOperationExecutor operations, AccessReviewReader campaigns,
    IImmutableContentStore content, TimeProvider clock, RestrictedApplicationVisibility visibility,
    AccessReviewQueueEligibility eligibility)
    : IRequestHandler<RecordBulkAccessDecision, OperationAcceptedResponse>
{
    const string CommandKind = nameof(RecordBulkAccessDecision);

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordBulkAccessDecision> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.DecisionBatchId == Uuid.Empty || request.ExpectedCampaignRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A bulk decision requires a batch id and a non-negative expected campaign revision.");
        var itemIds = request.ItemIds ?? [];
        var actor = AccessReviewActor.From(context);
        var campaign = await campaigns.ReadCampaignAsync(request.TenantId, request.CampaignId, request.DecisionBatchId,
            ct).ConfigureAwait(false);
        if (!await RestrictedAccessReviewVisibility.CanReadCampaignItemsAsync(visibility, request.TenantId,
                actor.UserId, campaign, itemIds, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound, "The review items were not found.");

        var now = clock.GetUtcNow();
        var stream = campaign.Campaign.Stream;
        var provenance = AccessReviewEffects.Root(request.DecisionBatchId, CommandKind, stream, actor.Reference,
            request.ExpectedCampaignRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? intent = null;
        if (campaign.Campaign.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            foreach (var itemId in itemIds.Distinct())
            {
                if (campaign.FindItem(itemId) is not { } item || campaign.CurrentReviewerMemberId(itemId) != actor.MemberId ||
                    campaign.Launched?.ProgramId is { } programId &&
                    !await eligibility.CanReviewAsync(request.TenantId, programId, actor.MemberId, item.SystemInstanceId, ct)
                        .ConfigureAwait(false))
                    return Failure(RequestErrorKind.NotFound,
                        "The review items were not found among the reviewer's current queue access.");
            }
            if (campaign.Campaign.PrepareBulkAdmission(request.ExpectedCampaignRevision) is { IsSuccess: false } blocked)
                return Result<OperationAcceptedResponse>.Failure(blocked.Error!);
            if (!AccessReviewVocabulary.IsBoundedText(request.Rationale, 4000))
                return Failure(RequestErrorKind.Validation,
                    "A decision is keep, modify, revoke, or unable_to_determine with a rationale of at most 4000 characters.");
            var preview = BulkAccessDecisions.Preview(campaign, itemIds, request.Decision, actor.MemberId, actor.UserId);
            if (!preview.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(preview.Error!);
            if (preview.Value!.Rejected.Count > 0 || preview.Value.Eligible.Count != itemIds.Count ||
                !string.Equals(preview.Value.PreviewToken, request.PreviewToken, StringComparison.Ordinal))
                return Failure(RequestErrorKind.Conflict,
                    "The bulk decision no longer matches its preview; preview it again.");
            if (preview.Value.Eligible.Any(item => campaign.State(item.ItemId) is not { Materialized: true }))
                return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A selected review item has not been materialized yet; retry shortly.", true));
            intent = await FreezeIntentAsync(request, campaign, provenance, [.. preview.Value.Eligible.Select(
                static item => item.ItemId)], now, ct).ConfigureAwait(false);
            response = await AccessReviewContent.AcceptedResponseAsync(content, request.TenantId,
                request.DecisionBatchId, now, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessReviewCampaign>(binding,
            () => new AccessReviewCampaign(request.TenantId, request.CampaignId, request.DecisionBatchId),
            owner => owner.Acceptance, owner => Prepare(owner, request.ExpectedCampaignRevision, response),
            (AccessReviewCampaign owner, ImmutableContentRef acknowledgment) => owner.AdmitBulkDecision(provenance,
                intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.AcknowledgmentAsync(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(AccessReviewCampaign owner, long expectedRevision,
        FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareBulkAdmission(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The campaign changed while the bulk decision was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    async Task<ImmutableContentRef> FreezeIntentAsync(RecordBulkAccessDecision request,
        AccessReviewCampaignSnapshot campaign, OperationProvenance provenance, IReadOnlyList<Uuid> itemIds,
        DateTimeOffset now, CancellationToken ct)
    {
        var tenantId = request.TenantId;
        var items = new SortedDictionary<string, BulkAccessDecisionItemIntent>(StreamKeyOrder.Instance);
        foreach (var itemId in itemIds)
        {
            var state = campaign.State(itemId)!;
            var stream = AccessReviewEffects.ItemStream(tenantId, request.CampaignId, itemId);
            var proofs = await ManifestAsync<BulkItemEligibilityProof>(tenantId, BulkItemEligibilityProof.Schema,
                [new BulkItemEligibilityProof(state.Latest!)], ct).ConfigureAwait(false);
            items.Add(stream.Resource, new BulkAccessDecisionItemIntent(itemId, stream, (ulong)state.Revision,
                state.CurrentReviewerMemberId, AccessReviewEffects.ReviewerEpisode(request.CampaignId, itemId), proofs));
        }

        var selection = await ManifestAsync(tenantId, BulkAccessDecisionItemIntent.Schema, [.. items.Values], ct)
            .ConfigureAwait(false);
        var header = new DurableIntentHeader(request.DecisionBatchId, provenance.Owner, provenance.Owner,
            provenance.Owner, provenance.Actor, provenance.Episode, provenance.OriginalExpectedRevision,
            provenance.AuthoredIntentSha256, now);
        return await AccessReviewContent.WriteAsync(content, tenantId, BulkAccessDecisionIntent.Schema,
            new BulkAccessDecisionIntent(header, request.CampaignId, (ulong)AccessReviewEffects.Generation,
                request.DecisionBatchId, (ulong)request.ExpectedCampaignRevision, request.PreviewToken, selection,
                request.Decision, request.Rationale.Trim()), ct).ConfigureAwait(false);
    }

    async Task<ImmutableSelectionManifest> ManifestAsync<T>(Uuid tenantId, string itemSchema, T[] items,
        CancellationToken ct) where T : notnull
    {
        var reference = await AccessReviewContent.WriteAsync<IReadOnlyList<T>>(content, tenantId, itemSchema, items, ct)
            .ConfigureAwait(false);
        return new ImmutableSelectionManifest(reference, itemSchema, items.Length, SelectionOrder.CanonicalKey,
            reference.Sha256);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<OperationAcceptedResponse>(kind, message);
}
