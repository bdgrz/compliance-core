using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Fences one item against a canceled batch before any hold is released: afterwards a delayed hold or apply of the
///     batch is refused, including on an item that was never held. The batch's accepted cancellation is found on the
///     authoritative batch; a cancellation that is not visible yet is transient and a batch that already published is a
///     terminal no-op (its decisions recover forward). An item fenced by an earlier delivery replays its fence.
/// </summary>
sealed class FenceAccessReviewItemDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    AccessReviewBulkVerifier verifier, TimeProvider clock)
    : IRequestHandler<FenceAccessReviewItemDecision, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<FenceAccessReviewItemDecision> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.CampaignId == Uuid.Empty ||
            command.ItemId == Uuid.Empty || command.DecisionBatchId != command.OperationId ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            command.Target != AccessReviewEffects.ItemStream(tenant, command.CampaignId, command.ItemId))
            return SourceUseReceiverSupport.Permanent("The fence names another item stream, operation or generation.");
        var bulk = await verifier.VerifyAsync(tenant, command.CampaignId, command.Source, command.Intent,
            command.OperationId, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<ReceiverOutcome>.Failure(bulk.Error);
        var intent = bulk.Intent!;
        if (await verifier.ItemOfAsync(command.Target.Realm, intent, command.Target.Resource, ct).ConfigureAwait(false)
                is not { } selected || selected.ItemId != command.ItemId)
            return SourceUseReceiverSupport.Permanent("The fence differs from the frozen bulk decision selection.");

        var batch = await reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, command.CampaignId,
            command.DecisionBatchId), ct).ConfigureAwait(false);
        if (batch.Published is { } published)
            return SourceUseReceiverSupport.Superseded("The batch already published; its decisions recover forward.",
                BulkReceiverSupport.Fact(batch.Stream, published));
        if (batch.CancellationAccepted is null)
            return SourceUseReceiverSupport.Transient("The batch cancellation is not visible yet.");

        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(FenceAccessReviewItemDecision),
            command.Source.EventId, intent.Header, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(Open(tenant, command), ct).ConfigureAwait(false);
                if (Decide(current, binding, command.DecisionBatchId) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(Open(tenant, command), item =>
                    {
                        if (Decide(item, binding, command.DecisionBatchId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = item.FenceBulkDecision(provenance, command.Intent, command.Generation);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(BulkReceiverSupport.Fact(item.Stream,
                                    item.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The item changed concurrently; rehydrate and decide again.
            }
        }
    }

    static AccessReviewItem Open(Uuid tenant, FenceAccessReviewItemDecision command) =>
        new(tenant, command.CampaignId, command.ItemId, command.CommandId);

    static Result<ReceiverOutcome>? Decide(AccessReviewItem item, AcceptedOperationBinding binding, Uuid batchId)
    {
        if (BulkReceiverSupport.Decide(item.Acceptance, item.Stream, binding) is { } accepted)
            return accepted;
        return item.FenceFor(batchId) is { } fence
            ? SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(item.Stream, fence))
            : null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
