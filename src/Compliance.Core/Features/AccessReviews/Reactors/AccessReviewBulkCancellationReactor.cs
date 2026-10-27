using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Reactors;

/// <summary>
///     Fences every selected item of a batch whose cancellation was accepted, including the items the batch never held,
///     before any hold is released. Each fence is the deterministic effect of the cancellation for that item, so a
///     restarted reactor converges on the same command. The items are fenced in canonical order; a fence that cannot
///     be applied yet is a transient failure, never a release.
/// </summary>
sealed partial class AccessReviewBulkCancellationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IImmutableContentStore content, IAggregateReader reader, AccessReviewManifests manifests, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewDecisionBatch.Area), WorkloadName),
        IReactorHandler<AccessReviewDecisionBatchCancellationAccepted>
{
    public const string WorkloadName = "AccessReviewBulkCancellationV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewDecisionBatchCancellationAccepted> context,
        CancellationToken ct)
    {
        await AccessReviewReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        var (tenant, intent, source) = await AccessReviewBulkReactions.ResolveAsync(context.Source.Stream.Realm,
            context.Trigger.Intent, content, reader, ct).ConfigureAwait(false);
        var items = await AccessReviewBulkReactions.ItemsAsync(manifests, context.Source.Stream.Realm, intent, ct)
            .ConfigureAwait(false);
        foreach (var selected in items)
        {
            var target = AccessReviewCompletionTarget.Of(tenant, intent.CampaignId, selected.ItemId).ToString();
            var command = new FenceAccessReviewItemDecision(
                BulkAccessDecisionProcess.ReceiverCommandId(source.EventId, nameof(FenceAccessReviewItemDecision),
                    target, intent.Header.Episode), source, selected.ItemOwner, intent.DecisionBatchId,
                AccessReviewEffects.Generation, null, context.Trigger.Intent, intent.CampaignId, selected.ItemId,
                intent.DecisionBatchId);
            var fenced = await bus.SendAsync(command, context, ct).ConfigureAwait(false);
            if (!fenced.IsSuccess)
                throw new ReactionCommandFailedException(fenced.Error!);
        }
    }
}
