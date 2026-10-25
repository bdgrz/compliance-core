using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Completes the accepted cancellation once every selected item carries its fence. The fence set the command
///     presents is only a claim: the receiver hydrates every item and finds its exact fence fact, so a cancellation
///     cannot complete while a delayed hold or apply could still land. A fence that is not visible yet is transient and
///     a mismatch is permanent. A batch that already published is a terminal no-op.
/// </summary>
sealed class CompleteAccessReviewDecisionBatchCancellationHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AccessReviewBulkVerifier verifier, AccessReviewManifests manifests,
    TimeProvider clock) : IRequestHandler<CompleteAccessReviewDecisionBatchCancellation, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteAccessReviewDecisionBatchCancellation> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.DecisionBatchId != command.OperationId ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            !Uuid.TryParse(command.Source.Stream.Resource, CultureInfo.InvariantCulture, out var campaignId) ||
            command.Target != AccessReviewEffects.BatchStream(tenant, campaignId, command.DecisionBatchId))
            return SourceUseReceiverSupport.Permanent(
                "The completion names another batch stream, operation or generation.");
        var bulk = await verifier.VerifyAsync(tenant, campaignId, command.Source, command.Intent, command.OperationId, ct)
            .ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<ReceiverOutcome>.Failure(bulk.Error);
        var intent = bulk.Intent!;
        var provenance = SourceUseReceiverSupport.Child(command.CommandId,
            nameof(CompleteAccessReviewDecisionBatchCancellation), command.Source.EventId, intent.Header, command.Target,
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var batch = await reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, campaignId, command.DecisionBatchId,
            command.CommandId), ct).ConfigureAwait(false);
        if (Decide(batch, binding) is { } early)
            return early;
        if (batch.CancellationAccepted is null)
            return SourceUseReceiverSupport.Transient("The batch cancellation is not visible yet.");

        try
        {
            if (await VerifyFencesAsync(tenant, campaignId, command, intent, ct).ConfigureAwait(false) is { } failure)
                return failure;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen bulk decision cannot be verified yet: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new AccessReviewDecisionBatch(tenant, campaignId,
                    command.DecisionBatchId, command.CommandId), fresh =>
                    {
                        if (Decide(fresh, binding) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CompleteCancellation(provenance, command.ItemFenceProofs);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(BulkReceiverSupport.Fact(fresh.Stream,
                                    fresh.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The batch changed concurrently; rehydrate and decide again.
            }
        }
    }

    async Task<Result<ReceiverOutcome>?> VerifyFencesAsync(Uuid tenant, Uuid campaignId,
        CompleteAccessReviewDecisionBatchCancellation command, BulkAccessDecisionIntent intent, CancellationToken ct)
    {
        var proofs = await AccessReviewContent.ReadAsync<IReadOnlyList<BulkItemFenceProof>>(content, tenant,
            command.ItemFenceProofs, ct).ConfigureAwait(false);
        var items = await manifests.BulkItemsAsync(command.Target.Realm, intent.Items, ct).ConfigureAwait(false);
        if (proofs.Count != items.Count)
            return SourceUseReceiverSupport.Permanent("The fence set is not the complete selection.");
        if (proofs.Select(static proof => proof.ItemId).Distinct().Count() != proofs.Count)
            return SourceUseReceiverSupport.Permanent("The fence set repeats an item.");
        var byItem = proofs.ToDictionary(static proof => proof.ItemId);
        foreach (var selected in items.Values)
        {
            if (!byItem.TryGetValue(selected.ItemId, out var proof))
                return SourceUseReceiverSupport.Permanent("The fence set omits a selected item.");
            var item = await reader.HydrateAsync(new AccessReviewItem(tenant, campaignId, selected.ItemId, selectedBatchId: intent.DecisionBatchId), ct)
                .ConfigureAwait(false);
            if (item.FenceFor(command.DecisionBatchId) is not { } fence)
                return SourceUseReceiverSupport.Transient($"The fence of item {selected.ItemId} is not visible yet.");
            if (proof.Fence != BulkReceiverSupport.Fact(item.Stream, fence))
                return SourceUseReceiverSupport.Permanent($"The fence proof of item {selected.ItemId} differs from its fence.");
        }

        return null;
    }

    static Result<ReceiverOutcome>? Decide(AccessReviewDecisionBatch batch, AcceptedOperationBinding binding)
    {
        if (BulkReceiverSupport.Decide(batch.Acceptance, batch.Stream, binding) is { } accepted)
            return accepted;
        if (batch.Canceled is { } canceled)
            return SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(batch.Stream, canceled));
        return batch.Published is { } published
            ? SourceUseReceiverSupport.Superseded("The batch already published; its decisions recover forward.",
                BulkReceiverSupport.Fact(batch.Stream, published))
            : null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
