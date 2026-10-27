using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Settles every item use of a batch that ended: a published batch settles with <c>irreversible_decision</c>, which
///     makes the decisions visible, and a canceled batch settles with <c>canceled</c> after every item fence. Each
///     settlement is the deterministic effect of the terminal fact for that item, so a restarted reactor and the process
///     driver converge on the same command. A settlement is recorded even for an item whose use was never acquired, so a
///     delayed hold is refused.
/// </summary>
sealed partial class AccessReviewBulkSettlementReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IImmutableContentStore content, IAggregateReader reader, AccessReviewManifests manifests, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewDecisionBatch.Area), WorkloadName),
        IReactorHandler<AccessReviewDecisionBatchPublished>,
        IReactorHandler<AccessReviewDecisionBatchCanceled>
{
    public const string WorkloadName = "AccessReviewBulkSettlementV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewDecisionBatchPublished> context, CancellationToken ct)
    {
        await AccessReviewReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        await SettleAsync(context.Source.Stream.Realm, context.Trigger.Intent, SourceUseSettlementDisposition.IrreversibleDecision,
            context, ct).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(IReactorContext<AccessReviewDecisionBatchCanceled> context, CancellationToken ct)
    {
        await AccessReviewReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        await SettleAsync(context.Source.Stream.Realm, context.Trigger.Intent, SourceUseSettlementDisposition.Canceled, context,
            ct).ConfigureAwait(false);
    }

    async ValueTask SettleAsync(string realm, ImmutableContentRef intentReference,
        SourceUseSettlementDisposition disposition, IExecutionContext context, CancellationToken ct)
    {
        var (tenant, intent, source) = await AccessReviewBulkReactions.ResolveAsync(realm, intentReference, content,
            reader, ct).ConfigureAwait(false);
        foreach (var selected in await AccessReviewBulkReactions.ItemsAsync(manifests, realm, intent, ct)
                     .ConfigureAwait(false))
        {
            var target = AccessReviewCompletionTarget.Of(tenant, intent.CampaignId, selected.ItemId).ToString();
            var settled = await bus.SendAsync(BulkAccessDecisionProcess.SettleCommand(source, intentReference, tenant,
                intent.CampaignId, intent.DecisionBatchId, selected.ItemId,
                BulkAccessDecisionProcess.ReceiverCommandId(source.EventId, nameof(SettleAccessReviewItemDecision),
                    target, intent.Header.Episode), disposition), context, ct).ConfigureAwait(false);
            if (!settled.IsSuccess)
                throw new ReactionCommandFailedException(settled.Error!);
        }
    }
}
