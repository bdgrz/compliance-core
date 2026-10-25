using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Publishes every provisional decision of the batch as one fact. The proof set the command presents is only a
///     claim: the receiver hydrates every selected item in canonical order and finds its exact hold and its exact
///     provisional decision (event and position), and refuses a partial, changed or fenced set. A provisional decision
///     that is not visible yet is transient and a mismatch is permanent. The terminal result is the complete list of
///     decisions the batch made. A canceled batch is a terminal no-op.
/// </summary>
sealed class PublishAccessReviewDecisionBatchHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AccessReviewBulkVerifier verifier, AccessReviewManifests manifests,
    TimeProvider clock) : IRequestHandler<PublishAccessReviewDecisionBatch, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<PublishAccessReviewDecisionBatch> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.DecisionBatchId != command.OperationId ||
            command.ExpectedRevision is not null ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The publication names another batch stream, operation or generation.");
        if (!Uuid.TryParse(command.Source.Stream.Resource, CultureInfo.InvariantCulture, out var campaignId))
            return SourceUseReceiverSupport.Permanent("The source is not an admitted bulk decision of a campaign.");
        var bulk = await verifier.VerifyAsync(tenant, campaignId, command.Source, command.Intent, command.OperationId, ct)
            .ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<ReceiverOutcome>.Failure(bulk.Error);
        var intent = bulk.Intent!;
        if (command.Target != AccessReviewEffects.BatchStream(tenant, campaignId, command.DecisionBatchId))
            return SourceUseReceiverSupport.Permanent("The publication names another batch stream.");

        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(PublishAccessReviewDecisionBatch),
            command.Source.EventId, intent.Header, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var batch = await reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, campaignId, command.DecisionBatchId,
            command.CommandId), ct).ConfigureAwait(false);
        if (Decide(batch, binding) is { } early)
            return early;
        if (batch.Accepted is null)
            return SourceUseReceiverSupport.Transient("The batch is not materialized yet.");

        ImmutableContentRef result;
        try
        {
            var verified = await VerifyProofsAsync(tenant, campaignId, command, intent, ct).ConfigureAwait(false);
            if (verified.Outcome is { } outcome)
                return outcome;
            var body = await content.WriteJsonAsync(command.Target.Realm, Uuid.CreateVersion5(command.CommandId, "result-body"),
                AccessReviewContent.BulkResultSchema, new BulkAccessDecisionResult(verified.Decisions!), ct)
                .ConfigureAwait(false);
            result = await content.WriteJsonAsync(command.Target.Realm, Uuid.CreateVersion5(command.CommandId, "result"),
                "mutation-result-content.v1", new MutationResultContent(AccessReviewContent.BulkResultSchema,
                    new StandardResponseBody(body)), ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Transient($"The frozen bulk decision cannot be verified yet: {exception.Message}");
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
                        var raised = fresh.Publish(provenance, command.ItemDecisionProofs, result);
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
                // The batch changed concurrently; rehydrate and decide again, including against a cancellation.
            }
        }
    }

    async Task<Verified> VerifyProofsAsync(Uuid tenant, Uuid campaignId, PublishAccessReviewDecisionBatch command,
        BulkAccessDecisionIntent intent, CancellationToken ct)
    {
        var proofs = await AccessReviewContent.ReadAsync<IReadOnlyList<BulkItemDecisionProof>>(content, tenant,
            command.ItemDecisionProofs, ct).ConfigureAwait(false);
        var items = await manifests.BulkItemsAsync(command.Target.Realm, intent.Items, ct).ConfigureAwait(false);
        if (proofs.Count != items.Count || intent.Items.Count != items.Count)
            return Verified.Fail(SourceUseReceiverSupport.Permanent("The proof set is not the complete selection."));
        var byItem = proofs.ToDictionary(static proof => proof.ItemId);
        var decisions = new List<AccessDecisionView>(items.Count);
        foreach (var (resource, selected) in items.OrderBy(static pair => pair.Key, Kernel.StreamKeyOrder.Instance))
        {
            if (!byItem.TryGetValue(selected.ItemId, out var proof))
                return Verified.Fail(SourceUseReceiverSupport.Permanent("The proof set omits a selected item."));
            var item = await reader.HydrateAsync(new AccessReviewItem(tenant, campaignId, selected.ItemId).ForSourceUse(candidate => candidate.BulkKey(command.DecisionBatchId)), ct)
                .ConfigureAwait(false);
            if (item.ItemId != selected.ItemId || item.Stream.Resource != resource)
                return Verified.Fail(SourceUseReceiverSupport.Permanent("The proof set names another item."));
            if (item.IsFenced(command.DecisionBatchId))
                return Verified.Fail(SourceUseReceiverSupport.Superseded("An item was fenced against the canceled batch.",
                    BulkReceiverSupport.Fact(item.Stream, item.FenceFor(command.DecisionBatchId)!)));
            var key = item.BulkKey(command.DecisionBatchId);
            if (item.ProvisionalFor(command.DecisionBatchId) is not { } provisional ||
                item.Uses.GrantOf(key) is not { } hold || item.Uses.Find(key) is not { IsActive: true })
                return Verified.Fail(SourceUseReceiverSupport.Transient(
                    $"The hold and provisional decision of item {selected.ItemId} are not visible yet."));
            if (proof.Decision != BulkReceiverSupport.Fact(item.Stream, provisional) || proof.Hold != hold.GrantEvent)
                return Verified.Fail(SourceUseReceiverSupport.Permanent(
                    $"The proof of item {selected.ItemId} differs from its applied decision."));
            var applied = await AccessReviewContent.ReadAsync<IReadOnlyList<AccessDecisionView>>(content, tenant,
                provisional.DecisionsManifest, ct).ConfigureAwait(false);
            decisions.AddRange(applied);
        }

        return new Verified(null, decisions);
    }

    static Result<ReceiverOutcome>? Decide(AccessReviewDecisionBatch batch, AcceptedOperationBinding binding)
    {
        if (BulkReceiverSupport.Decide(batch.Acceptance, batch.Stream, binding) is { } accepted)
            return accepted;
        if (batch.Published is { } published)
            return SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(batch.Stream, published));
        return batch.CancellationAccepted is { } canceled
            ? SourceUseReceiverSupport.Superseded("The bulk decision was canceled; nothing is published.",
                BulkReceiverSupport.Fact(batch.Stream, canceled))
            : null;
    }

    sealed record Verified(Result<ReceiverOutcome>? Outcome, IReadOnlyList<AccessDecisionView>? Decisions)
    {
        public static Verified Fail(Result<ReceiverOutcome> outcome) => new(outcome, null);
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
