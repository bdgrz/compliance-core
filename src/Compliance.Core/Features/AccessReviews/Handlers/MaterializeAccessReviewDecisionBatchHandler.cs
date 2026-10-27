using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Materializes the decision batch at its own revision zero from the exact admitted intent. The admitted bulk
///     decision is found on the authoritative campaign and its frozen intent is read from verified content; nothing a
///     command states is trusted until the campaign confirms it, and no current campaign or item state is consulted.
///     An exact replay returns the original fact; a batch materialized with a different intent is a permanent
///     conflict. A fact that is not visible yet is transient.
/// </summary>
sealed class MaterializeAccessReviewDecisionBatchHandler(IAggregateReader reader, IAggregateExecutor executor,
    AccessReviewBulkVerifier verifier, TimeProvider clock)
    : IRequestHandler<MaterializeAccessReviewDecisionBatch, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializeAccessReviewDecisionBatch> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.ExpectedRevision is not null ||
            command.CampaignId == Uuid.Empty || command.DecisionBatchId != command.OperationId ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The materialization names another generation, operation or realm.");
        var target = AccessReviewEffects.BatchStream(tenant, command.CampaignId, command.DecisionBatchId);
        if (command.Target != target)
            return SourceUseReceiverSupport.Permanent("The materialization names another batch stream.");
        var bulk = await verifier.ReadNativeAsync(tenant, command.CampaignId, command.Source, command.Intent,
            command.OperationId, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<ReceiverOutcome>.Failure(bulk.Error);
        var operation = bulk.Admitted!.Operation;
        var header = AccessReviewIntentBindings.HeaderOf(operation);
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(MaterializeAccessReviewDecisionBatch),
            command.Source.EventId, header, target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<AccessReviewDecisionBatch, BulkAccessDecisionIntent>(reader,
            () => new AccessReviewDecisionBatch(tenant, command.CampaignId, command.DecisionBatchId, command.CommandId),
            owner => Decide(owner, binding, command, provenance), async (_, token) =>
            {
                var prepared = await verifier.PrepareAsync(tenant, bulk, token).ConfigureAwait(false);
                return prepared.Error is not null ? Result<BulkAccessDecisionIntent>.Failure(prepared.Error)
                    : Result<BulkAccessDecisionIntent>.Success(prepared.Intent!);
            }, ct).ConfigureAwait(false);
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
                if (Decide(current, binding, command, provenance) is { } early)
                    return early;

                var step = await executor.ExecuteAsync(new AccessReviewDecisionBatch(tenant, command.CampaignId,
                    command.DecisionBatchId, command.CommandId), batch =>
                    {
                        if (Decide(batch, binding, command, provenance) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        if (batch.Accepted is not null)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Transient(
                                    "The batch was materialized concurrently; the replay is decided again."))));
                        var raised = batch.Materialize(provenance, command.Intent);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(BulkReceiverSupport.Fact(batch.Stream,
                                    batch.Accepted!)))))
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

    static Result<ReceiverOutcome>? Decide(AccessReviewDecisionBatch batch, AcceptedOperationBinding binding,
        MaterializeAccessReviewDecisionBatch command, OperationProvenance provenance)
    {
        if (batch.Accepted is { } existing &&
            (existing.Intent != command.Intent || existing.Operation.RootOperationId != command.OperationId ||
                existing.Operation.CausationEventId != command.Source.EventId || existing.Operation.FirstOwner != provenance.FirstOwner))
            return SourceUseReceiverSupport.Permanent("The batch is already materialized with another exact source or frozen intent.");
        if (BulkReceiverSupport.Decide(batch.Acceptance, batch.Stream, binding) is { } known)
            return known;
        if (batch.Accepted is not { } accepted)
            return null;
        return AcceptedOperationBinding.Of(accepted.Operation with { LogicalId = provenance.LogicalId }) == binding
            ? SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(batch.Stream, accepted))
            : SourceUseReceiverSupport.Permanent("The materialized batch has another exact native source binding.");
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
