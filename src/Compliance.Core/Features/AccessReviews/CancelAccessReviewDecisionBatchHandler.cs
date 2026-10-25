using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Accepts the cancellation of the exact materialized batch. It is the fence proof the item fences, the cancellation
///     completion and the item settlements follow; no source use is settled before it. A batch that already published
///     is an irreversible decision: the cancel is a terminal no-op and the process recovers forward. A batch that is not
///     materialized yet is a transient wait.
/// </summary>
sealed class CancelAccessReviewDecisionBatchHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CancelAccessReviewDecisionBatch, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CancelAccessReviewDecisionBatch> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.DecisionBatchId == Uuid.Empty || command.CampaignId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            command.Target != AccessReviewEffects.BatchStream(tenant, command.CampaignId, command.DecisionBatchId))
            return SourceUseReceiverSupport.Permanent("The cancel names another batch stream, operation or generation.");

        var batch = await reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, command.CampaignId,
            command.DecisionBatchId, command.CommandId), ct).ConfigureAwait(false);
        if (batch.Accepted is not { } accepted)
            return SourceUseReceiverSupport.Transient("The batch is not materialized yet.");
        if (command.ExpectedRevision != accepted.Revision)
            return SourceUseReceiverSupport.Permanent("The cancel differs from the materialized batch.");
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenant, command.CampaignId,
            selectedRootId: command.DecisionBatchId), ct).ConfigureAwait(false);
        if (campaign.AdmittedFor(command.DecisionBatchId) is not { } admitted)
            return SourceUseReceiverSupport.Transient("The admitted bulk decision is not visible yet.");
        if (admitted.Intent != accepted.Intent || accepted.Operation.RootOperationId != command.DecisionBatchId ||
            accepted.Operation.CausationEventId != admitted.Metadata.EventId)
            return SourceUseReceiverSupport.Permanent("The materialized batch belongs to another exact admitted bulk decision.");
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(CancelAccessReviewDecisionBatch),
            accepted.Metadata.EventId, AccessReviewIntentBindings.HeaderOf(admitted.Operation), batch.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<AccessReviewDecisionBatch, Verified>(reader,
            () => new AccessReviewDecisionBatch(tenant, command.CampaignId, command.DecisionBatchId, command.CommandId),
            owner => Decide(owner, binding, command, false),
            (_, token) => PrepareAsync(tenant, campaign, admitted, command, token), ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, command.CampaignId,
                    command.DecisionBatchId, command.CommandId), ct).ConfigureAwait(false);
                if (Decide(current, binding, command, true) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new AccessReviewDecisionBatch(tenant, command.CampaignId,
                    command.DecisionBatchId, command.CommandId), fresh =>
                    {
                        if (Decide(fresh, binding, command, true) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.AcceptCancellation(provenance, command.FailureProof);
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

    async ValueTask<Result<Verified>> PrepareAsync(Uuid tenant, AccessReviewCampaign campaign,
        AccessReviewDecisionBatchAdmitted admitted, CancelAccessReviewDecisionBatch command, CancellationToken ct)
    {
        try
        {
            var intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenant, admitted.Intent, ct)
                .ConfigureAwait(false);
            var failure = await AccessReviewContent.ReadAsync<AccessReviewBulkFailure>(content, tenant,
                command.FailureProof, ct).ConfigureAwait(false);
            AccessReviewIntentBindings.Require(intent.Header, admitted.Operation);
            if (intent.Header.RootOperationId != command.DecisionBatchId || intent.CampaignId != command.CampaignId ||
                failure.Evidence != NativeFactReference.Of(campaign.Stream, admitted))
                return Result<Verified>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The failure proof belongs to another admitted bulk decision.", false));
            if (failure.ItemId is { } failedItem)
            {
                var selected = await new AccessReviewManifests(content).BulkItemsAsync(command.Target.Realm,
                    intent.Items, ct).ConfigureAwait(false);
                if (!selected.Values.Any(item => item.ItemId == failedItem))
                    return Result<Verified>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The failure proof names an item outside the frozen selection.", false));
            }
            return Result<Verified>.Success(new Verified());
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<Verified>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen bulk decision cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    static Result<ReceiverOutcome>? Decide(AccessReviewDecisionBatch batch, AcceptedOperationBinding binding,
        CancelAccessReviewDecisionBatch command, bool prepared)
    {
        if (!prepared && batch.CancellationAccepted?.SourceProofs != command.FailureProof)
            return null;
        if (BulkReceiverSupport.Decide(batch.Acceptance, batch.Stream, binding) is { } accepted)
            return accepted;
        if (batch.Published is { } published)
            return SourceUseReceiverSupport.Superseded("The batch already published; its decisions recover forward.",
                BulkReceiverSupport.Fact(batch.Stream, published));
        return batch.CancellationAccepted is { } canceled
            ? SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(batch.Stream, canceled))
            : null;
    }

    sealed record Verified;

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
