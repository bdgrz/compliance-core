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
///     The item pair of the generic settle receiver. The terminal fact is resolved from the campaign: a closed
///     campaign requires <c>parent_closed</c>, which installs the closure fence on the item, and a canceled completion
///     requires <c>canceled</c>, which clears only this use after the parent fence. A terminal fact that is not
///     visible yet is a transient wait, never an inferred cancellation.
/// </summary>
sealed class SettleAccessReviewItemCampaignCompletionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, AccessReviewCompletionVerifier verifier)
    : SettleSourceUseHandler<AccessReviewItem, SettleAccessReviewItemCampaignCompletionUse>(reader, executor, content,
        clock)
{
    protected override AccessReviewItem Open(SettleAccessReviewItemCampaignCompletionUse request,
        Uuid selectedCommandId) => new(Tenant(request.Target),
        request.CampaignId, request.SourceOwnerId, selectedCommandId);

    protected override ReservationKey ReceiptSelection(SettleAccessReviewItemCampaignCompletionUse request) =>
        new(request.OperationId, AccessReviewEffects.CampaignStream(Tenant(request.Target), request.CampaignId), (ulong)AccessReviewEffects.Generation, request.Target, SourceUseReservations.PurposeName(SourceUsePurpose.CampaignCompletion));

    protected override async ValueTask<Result<SourceUseSource>?> VerifyReceiptSourceAsync(SettleAccessReviewItemCampaignCompletionUse request,
        AccessReviewItem owner, CancellationToken ct)
    {
        var tenant = Tenant(request.Target);
        if (request.Generation != AccessReviewEffects.Generation || request.SourceOwnerId != owner.ItemId)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The request differs from its selected Access source-use owner or generation.", false));
        var native = await verifier.ReadNativeAsync(tenant, request.CampaignId, request.Source, request.Intent,
            request.OperationId, Uuid.Empty, ct).ConfigureAwait(false);
        if (native.Error is not null)
            return Result<SourceUseSource>.Failure(native.Error);
        var campaign = native.Campaign!;
        var parent = native.Accepted!.Operation;
        var key = ReceiptSelection(request)!;
        var recorded = await AccessReviewSourceUseReceipts.ReadAsync(Content, owner, key, parent, request.Source,
            request.Intent, SourceUsePurpose.CampaignCompletion, true, false, ct).ConfigureAwait(false);
        if (recorded is null)
            return null;
        var result = recorded.Value;
        if (!result.IsSuccess)
            return Result<SourceUseSource>.Failure(result.Error!);
        var intent = result.Value!;
        var disposition = campaign.ClosedFor(request.OperationId) is not null ? SourceUseSettlementDisposition.ParentClosed
            : campaign.CanceledFor(request.OperationId) is not null ? SourceUseSettlementDisposition.Canceled
            : (SourceUseSettlementDisposition?)null;
        if (disposition is null)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Neither the closed campaign nor the cancellation fence is visible yet.", true));
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, intent.ParentIntent, key,
            intent.Requirement, nameof(SettleAccessReviewItemCampaignCompletionUse), disposition, null));
    }

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        SettleAccessReviewItemCampaignCompletionUse request, CancellationToken ct)
    {
        var tenant = Uuid.Parse(request.Target.Realm, CultureInfo.InvariantCulture);
        var completion = await verifier.VerifyAsync(tenant, request.CampaignId, request.Source, request.Intent,
            request.OperationId, Uuid.Empty, ct).ConfigureAwait(false);
        if (completion.Error is not null)
            return Result<SourceUseSource>.Failure(completion.Error);
        var (campaign, intent) = (completion.Campaign!, completion.Intent!);
        var requirement = await verifier.RequirementOfAsync(request.Target.Realm, intent, request.Target.Resource, ct)
            .ConfigureAwait(false);
        if (requirement is null ||
            request.Generation != AccessReviewEffects.Generation)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The settlement differs from the frozen item use requirement.", false));
        var disposition = campaign.ClosedFor(request.OperationId) is not null
            ? SourceUseSettlementDisposition.ParentClosed
            : campaign.CanceledFor(request.OperationId) is not null
                ? SourceUseSettlementDisposition.Canceled
                : (SourceUseSettlementDisposition?)null;
        return disposition is null
            ? Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Neither the closed campaign nor the cancellation fence is visible yet.", true))
            : Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
                AccessReviewCompletionVerifier.UseKey(request.OperationId, campaign, requirement), requirement,
                nameof(SettleAccessReviewItemCampaignCompletionUse), disposition, null));
    }

    protected override Func<AccessReviewItem, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        SettleOperation(SourceUseSettlementDisposition.ParentClosed);

    protected override Func<AccessReviewItem, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation(
        SourceUseSettlementDisposition disposition) =>
        (AccessReviewItem item, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            item.SettleCampaignCompletionUse(provenance, intent, generation, disposition);
    static Uuid Tenant(EventStreamAddress stream) => Uuid.Parse(stream.Realm, CultureInfo.InvariantCulture);

}
