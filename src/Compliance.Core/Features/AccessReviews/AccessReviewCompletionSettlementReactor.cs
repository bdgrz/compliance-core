using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Settles every item use of a canceled completion. The cancellation is committed first, so a delayed acquisition
///     always meets the fence before any use is cleared; each settlement is the deterministic effect of the process
///     for that item, so a restarted reactor and the process driver converge on the same command. A settlement is
///     recorded even for an item whose use was never acquired, so a delayed acquisition is refused. A process record
///     that is not visible yet is a transient failure, never a release.
/// </summary>
sealed partial class AccessReviewCompletionSettlementReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IProcessRecordStore records)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewCampaign.Area), WorkloadName),
        IReactorHandler<AccessReviewCampaignCompletionCanceled>
{
    public const string WorkloadName = "AccessReviewCompletionSettlementV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewCampaignCompletionCanceled> context,
        CancellationToken ct)
    {
        var key = new ProcessKey(AccessReviewEffects.CompletionProcessName, context.Source.Stream.Realm,
            context.Trigger.Operation.RootOperationId);
        var record = await records.ReadAsync(key, ct).ConfigureAwait(false);
        if (record is null)
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                "The completion process record is not visible yet.", true));
        foreach (var target in record.Targets)
        {
            var settled = await bus.SendAsync(AccessReviewCompletionProcess.SettleCommand(record, target,
                AccessReviewCompletionProcess.ReceiverCommandId(record.Source.EventId,
                    nameof(SettleAccessReviewItemCampaignCompletionUse), target, record.Episode),
                SourceUseSettlementDisposition.Canceled), context, ct).ConfigureAwait(false);
            if (!settled.IsSuccess)
                throw new ReactionCommandFailedException(settled.Error!);
        }
    }
}
