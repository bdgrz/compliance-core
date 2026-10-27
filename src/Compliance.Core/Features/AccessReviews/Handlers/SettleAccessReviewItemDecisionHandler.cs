using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     The item pair of the generic settle receiver for a bulk decision. The terminal fact is resolved from the batch:
///     a published batch requires <c>irreversible_decision</c>, which makes the item's provisional decision visible (and
///     is refused for an item that holds none), and a canceled batch requires <c>canceled</c>, which clears only this use
///     after the item fence. A terminal fact that is not visible yet is a transient wait, never an inferred
///     cancellation.
/// </summary>
sealed class SettleAccessReviewItemDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, AccessReviewBulkVerifier verifier)
    : SettleSourceUseHandler<AccessReviewItem, SettleAccessReviewItemDecision>(reader, executor, content, clock)
{
    readonly IAggregateReader _reader = reader;

    protected override AccessReviewItem Open(SettleAccessReviewItemDecision request, Uuid selectedCommandId) =>
        new(Tenant(request.Target), request.CampaignId, request.SourceOwnerId, selectedCommandId, request.OperationId);

    protected override ReservationKey ReceiptSelection(SettleAccessReviewItemDecision request) =>
        AccessReviewEffects.BulkUseKey(request.OperationId, AccessReviewEffects.CampaignStream(Tenant(request.Target), request.CampaignId), request.Target);

    protected override async ValueTask<Result<SourceUseSource>?> VerifyReceiptSourceAsync(SettleAccessReviewItemDecision request,
        AccessReviewItem owner, CancellationToken ct)
    {
        var tenant = Tenant(request.Target);
        if (request.Generation != AccessReviewEffects.Generation || request.SourceOwnerId != owner.ItemId || request.DecisionBatchId != request.OperationId)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The request differs from its selected Access source-use owner or generation.", false));
        var native = await verifier.ReadNativeAsync(tenant, request.CampaignId, request.Source, request.Intent,
            request.OperationId, ct).ConfigureAwait(false);
        if (native.Error is not null)
            return Result<SourceUseSource>.Failure(native.Error);
        var campaign = native.Campaign!;
        var parent = native.Admitted!.Operation;
        var key = ReceiptSelection(request)!;
        var recorded = await AccessReviewSourceUseReceipts.ReadAsync(Content, owner, key, parent, request.Source,
            request.Intent, SourceUsePurpose.BulkDecision, true, false, ct).ConfigureAwait(false);
        if (recorded is null)
            return null;
        var result = recorded.Value;
        if (!result.IsSuccess)
            return Result<SourceUseSource>.Failure(result.Error!);
        var intent = result.Value!;
        var batch = await _reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, request.CampaignId,
            request.DecisionBatchId), ct).ConfigureAwait(false);
        var disposition = batch.Published is not null ? SourceUseSettlementDisposition.IrreversibleDecision
            : batch.Canceled is not null ? SourceUseSettlementDisposition.Canceled : (SourceUseSettlementDisposition?)null;
        if (disposition is null)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Neither the published batch nor its completed cancellation is visible yet.", true));
        if (disposition == SourceUseSettlementDisposition.IrreversibleDecision && owner.ProvisionalFor(request.DecisionBatchId) is null)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The published batch has no applied decision on this item.", false));
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, intent.ParentIntent, key,
            intent.Requirement, nameof(SettleAccessReviewItemDecision), disposition, null));
    }

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(SettleAccessReviewItemDecision request,
        CancellationToken ct)
    {
        var tenant = Tenant(request.Target);
        var bulk = await verifier.VerifyAsync(tenant, request.CampaignId, request.Source, request.Intent,
            request.OperationId, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<SourceUseSource>.Failure(bulk.Error);
        var (campaign, intent) = (bulk.Campaign!, bulk.Intent!);
        var selected = await verifier.ItemOfAsync(request.Target.Realm, intent, request.Target.Resource, ct)
            .ConfigureAwait(false);
        if (selected is null || selected.ItemId != request.SourceOwnerId ||
            request.DecisionBatchId != request.OperationId || request.Generation != AccessReviewEffects.Generation)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The settlement differs from the frozen bulk decision selection.", false));
        NativeFactReference head;
        try
        {
            head = await verifier.HeadOfAsync(tenant, selected, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen item head cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        var batch = await _reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, request.CampaignId,
            request.DecisionBatchId), ct).ConfigureAwait(false);
        var disposition = batch.Published is not null ? SourceUseSettlementDisposition.IrreversibleDecision
            : batch.Canceled is not null ? SourceUseSettlementDisposition.Canceled
            : (SourceUseSettlementDisposition?)null;
        if (disposition is null)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Neither the published batch nor its completed cancellation is visible yet.", true));
        if (disposition == SourceUseSettlementDisposition.IrreversibleDecision)
        {
            var item = await _reader.HydrateAsync(new AccessReviewItem(tenant, request.CampaignId,
                request.SourceOwnerId, selectedBatchId: request.DecisionBatchId), ct).ConfigureAwait(false);
            if (item.ProvisionalFor(request.DecisionBatchId) is null)
                return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The published batch has no applied decision on this item.", false));
        }

        var requirement = new SourceUseRequirement(request.Target, head, selected.ExpectedItemRevision,
            SourceUsePurpose.BulkDecision, selected.EligibilityProofs.Content);
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            AccessReviewEffects.BulkUseKey(request.OperationId, campaign.Stream, request.Target), requirement,
            nameof(SettleAccessReviewItemDecision), disposition, null));
    }

    protected override Func<AccessReviewItem, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        SettleOperation(SourceUseSettlementDisposition.IrreversibleDecision);

    protected override Func<AccessReviewItem, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation(
        SourceUseSettlementDisposition disposition) =>
        (AccessReviewItem item, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            item.SettleBulkDecision(provenance, intent, generation, disposition);

    static Uuid Tenant(EventStreamAddress stream) => Uuid.Parse(stream.Realm, CultureInfo.InvariantCulture);
}
