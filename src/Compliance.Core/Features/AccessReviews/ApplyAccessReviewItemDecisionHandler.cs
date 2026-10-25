using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Applies the batch's shared decision to one held item as a provisional fact. The decision is built only from the
///     frozen intent (the reviewer, the shared rationale, the admission time and the preview token), so a replay or a
///     resumed process appends the identical decision. It stays hidden until the batch publishes. An item fenced
///     against a canceled batch, or whose hold is gone, refuses it; a hold that is not visible yet is transient.
/// </summary>
sealed class ApplyAccessReviewItemDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AccessReviewBulkVerifier verifier, TimeProvider clock)
    : IRequestHandler<ApplyAccessReviewItemDecision, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyAccessReviewItemDecision> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.CampaignId == Uuid.Empty ||
            command.ItemId == Uuid.Empty || command.DecisionBatchId != command.OperationId ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            command.Target != AccessReviewEffects.ItemStream(tenant, command.CampaignId, command.ItemId))
            return SourceUseReceiverSupport.Permanent("The apply names another item stream, operation or generation.");
        var bulk = await verifier.VerifyAsync(tenant, command.CampaignId, command.Source, command.Intent,
            command.OperationId, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<ReceiverOutcome>.Failure(bulk.Error);
        var (campaign, intent) = (bulk.Campaign!, bulk.Intent!);
        var selected = await verifier.ItemOfAsync(command.Target.Realm, intent, command.Target.Resource, ct)
            .ConfigureAwait(false);
        if (selected is null || selected.ItemId != command.ItemId || campaign.Launched is not { } launched)
            return SourceUseReceiverSupport.Permanent("The apply differs from the frozen bulk decision selection.");

        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(ApplyAccessReviewItemDecision),
            command.Source.EventId, intent.Header, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var view = new AccessDecisionView(Uuid.CreateVersion5(command.OperationId, $"bulk-decision:{command.ItemId}"),
            command.ItemId, intent.Decision, intent.Rationale, selected.ReviewerMemberId, intent.Header.OriginalActor,
            intent.Header.AcceptedAt, intent.PreviewToken, intent.Header.AcceptedAt > launched.Deadline, null);
        var decisions = await content.WriteJsonAsync<IReadOnlyList<AccessDecisionView>>(command.Target.Realm,
            Uuid.CreateVersion5(command.CommandId, "bulk-decisions"), AccessReviewContent.DecisionsSchema, [view], ct)
            .ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(Open(tenant, command), ct).ConfigureAwait(false);
                if (Decide(current, binding, command.OperationId) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(Open(tenant, command), item =>
                    {
                        if (Decide(item, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = item.ApplyBulkDecision(provenance, decisions, command.Intent);
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
                // The item changed concurrently; rehydrate and decide again, including against a fence.
            }
        }
    }

    static AccessReviewItem Open(Uuid tenant, ApplyAccessReviewItemDecision command)
    {
        var owner = new AccessReviewItem(tenant, command.CampaignId, command.ItemId, command.CommandId, command.DecisionBatchId);
        return owner.ForSourceUses(owner.BulkKey(command.OperationId));
    }

    static Result<ReceiverOutcome>? Decide(AccessReviewItem item, AcceptedOperationBinding binding, Uuid batchId)
    {
        if (BulkReceiverSupport.Decide(item.Acceptance, item.Stream, binding) is { } accepted)
            return accepted;
        if (item.ProvisionalFor(batchId) is { } existing)
            return SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(item.Stream, existing));
        if (item.FenceFor(batchId) is { } fence)
            return SourceUseReceiverSupport.Superseded("The item was fenced against the canceled bulk decision.",
                BulkReceiverSupport.Fact(item.Stream, fence));
        if (item.Opened is null || item.Uses.Find(item.BulkKey(batchId)) is not { IsActive: true })
            return item.Uses.Find(item.BulkKey(batchId)) is { IsSettled: true } settled
                ? SourceUseReceiverSupport.Superseded("The bulk decision use of the item already settled.",
                    BulkReceiverSupport.Fact(item.Stream, settled.SettleFact!))
                : SourceUseReceiverSupport.Transient("The hold of the item is not visible yet.");
        return null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
