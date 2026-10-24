using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

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
        var bulk = await verifier.VerifyAsync(tenant, command.CampaignId, command.Source, command.Intent,
            command.OperationId, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<ReceiverOutcome>.Failure(bulk.Error);
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(MaterializeAccessReviewDecisionBatch),
            command.Source.EventId, bulk.Intent!.Header, target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new AccessReviewDecisionBatch(tenant, command.CampaignId,
                    command.DecisionBatchId, command.CommandId), ct).ConfigureAwait(false);
                if (BulkReceiverSupport.Decide(current.Acceptance, current.Stream, binding) is { } early)
                    return early;
                if (current.Accepted is { } existing)
                    return existing.Intent == command.Intent
                        ? SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(current.Stream, existing))
                        : SourceUseReceiverSupport.Permanent(
                            "The batch is already materialized with a different frozen intent.");

                var step = await executor.ExecuteAsync(new AccessReviewDecisionBatch(tenant, command.CampaignId,
                    command.DecisionBatchId, command.CommandId), batch =>
                    {
                        if (BulkReceiverSupport.Decide(batch.Acceptance, batch.Stream, binding) is { } decided)
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

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
