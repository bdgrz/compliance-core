using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The item pair of the generic acquire receiver for a bulk decision: it holds the exact head of the item that the
///     preview froze into the admitted intent. An item that changed since, that is closed, or that no longer carries the
///     frozen head rejects the batch and cancels it through the fences; an item already fenced against the batch is a
///     terminal no-op. While the hold is active the item refuses every other change, and a campaign completion waits for
///     it. The batch is never compared with the campaign revision.
/// </summary>
sealed class ReserveAccessReviewItemDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, AccessReviewBulkVerifier verifier,
    AccessReviewBulkFences fences)
    : AuthorizeSourceUseHandler<AccessReviewItem, ReserveAccessReviewItemDecision>(reader, executor, content, clock)
{
    readonly IAggregateReader _reader = reader;

    protected override AccessReviewItem Open(ReserveAccessReviewItemDecision request, Uuid selectedCommandId) =>
        new(Tenant(request.Target), request.CampaignId, request.SourceOwnerId, selectedCommandId, request.OperationId, request.SourceContentEventId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(ReserveAccessReviewItemDecision request,
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
                "The hold differs from the frozen bulk decision selection.", false));
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

        if (head.EventId != request.SourceContentEventId || selected.ExpectedItemRevision != request.ExpectedContentRevision)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The hold differs from the frozen item head.", false));
        var requirement = new SourceUseRequirement(request.Target, head, selected.ExpectedItemRevision,
            SourceUsePurpose.BulkDecision, selected.EligibilityProofs.Content);
        var batch = await _reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, request.CampaignId,
            request.DecisionBatchId), ct).ConfigureAwait(false);
        var superseded = batch.CancellationAccepted is not null ? "The bulk decision was canceled."
            : batch.Published is not null ? "The bulk decision already published."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            AccessReviewEffects.BulkUseKey(request.OperationId, campaign.Stream, request.Target), requirement,
            nameof(ReserveAccessReviewItemDecision), null, superseded));
    }

    protected override ValueTask<Result> VerifyPinnedAsync(AccessReviewItem owner, SourceUseRequirement requirement,
        CancellationToken ct)
    {
        if (owner.Opened is null)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The review item is not materialized yet.", true)));
        if (owner.IsClosed)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A completed campaign cannot change.", false)));
        if (!IsPinned(owner, requirement))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The item changed after the bulk decision was previewed.", false)));
        return ValueTask.FromResult(owner.Uses.HasActiveUse
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another operation holds this item; retry after it settles.", true))
            : Result.Success);
    }

    protected override bool IsPinned(AccessReviewItem owner, SourceUseRequirement requirement) =>
        !owner.IsClosed && owner.Latest is { } latest && latest.Metadata!.EventId == requirement.ContentEvent.EventId &&
        (ulong)owner.Revision == requirement.ResourceRevision;

    protected override string? FencedReason(AccessReviewItem owner, ReservationKey key) =>
        owner.IsFenced(key.RootOperationId) ? "The item was fenced against the canceled bulk decision." : null;

    protected override Func<AccessReviewItem, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (AccessReviewItem item, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            item.AuthorizeBulkDecision(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(ReserveAccessReviewItemDecision request,
        SourceUseSource source, string reason, IExecutionContext context, CancellationToken ct)
    {
        var bulk = await verifier.VerifyAsync(Tenant(request.Target), request.CampaignId, request.Source,
            request.Intent, request.OperationId, ct).ConfigureAwait(false);
        return bulk.Error is not null
            ? Result<ReceiverOutcome>.Failure(bulk.Error)
            : await fences.CancelAsync(bulk, request.Source, reason, request.SourceOwnerId, context, ct)
                .ConfigureAwait(false);
    }

    static Uuid Tenant(EventStreamAddress stream) => Uuid.Parse(stream.Realm, CultureInfo.InvariantCulture);
}
