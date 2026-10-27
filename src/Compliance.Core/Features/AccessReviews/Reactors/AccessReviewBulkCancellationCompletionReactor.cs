using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Reactors;

/// <summary>
///     Completes the cancellation of a batch once every selected item carries its fence. The complete fence proof set is
///     read from the item streams; an item whose fence is not visible yet is a transient failure, so the batch is never
///     reported canceled while a delayed hold or apply could still land.
/// </summary>
sealed partial class AccessReviewBulkCancellationCompletionReactor(IProjectionCheckpointStore checkpoints,
    IRequestBus bus, IImmutableContentStore content, IAggregateReader reader, AccessReviewManifests manifests, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewDecisionBatch.Area), WorkloadName),
        IReactorHandler<AccessReviewDecisionBatchCancellationAccepted>
{
    public const string WorkloadName = "AccessReviewBulkCancellationCompletionV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewDecisionBatchCancellationAccepted> context,
        CancellationToken ct)
    {
        await AccessReviewReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        var realm = context.Source.Stream.Realm;
        var (tenant, intent, source) = await AccessReviewBulkReactions.ResolveAsync(realm, context.Trigger.Intent,
            content, reader, ct).ConfigureAwait(false);
        var items = await AccessReviewBulkReactions.ItemsAsync(manifests, realm, intent, ct).ConfigureAwait(false);
        var proofs = new List<BulkItemFenceProof>(items.Count);
        foreach (var selected in items)
        {
            var item = await reader.HydrateAsync(new AccessReviewItem(tenant, intent.CampaignId, selected.ItemId, selectedBatchId: intent.DecisionBatchId), ct)
                .ConfigureAwait(false);
            if (item.FenceFor(intent.DecisionBatchId) is not { } fence)
                throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                    $"The fence of item {selected.ItemId} is not visible yet.", true));
            proofs.Add(new BulkItemFenceProof(selected.ItemId, NativeFactReference.Of(item.Stream, fence)));
        }

        var commandId = BulkAccessDecisionProcess.ReceiverCommandId(source.EventId,
            nameof(CompleteAccessReviewDecisionBatchCancellation), null, intent.Header.Episode);
        var proofSet = await content.WriteJsonAsync<IReadOnlyList<BulkItemFenceProof>>(realm,
            Uuid.CreateVersion5(commandId, "item-fence-proofs"), BulkItemFenceProof.Schema, proofs, ct)
            .ConfigureAwait(false);
        var completed = await bus.SendAsync(new CompleteAccessReviewDecisionBatchCancellation(commandId, source,
            context.Source.Stream, intent.DecisionBatchId, AccessReviewEffects.Generation, null, context.Trigger.Intent,
            intent.DecisionBatchId, proofSet), context, ct).ConfigureAwait(false);
        if (!completed.IsSuccess)
            throw new ReactionCommandFailedException(completed.Error!);
    }
}
