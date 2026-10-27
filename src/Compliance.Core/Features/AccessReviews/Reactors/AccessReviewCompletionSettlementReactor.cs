using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.NativeOperations;
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
    IProcessRecordStore records, IAggregateReader reader, ISourceFactResolver sources, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewCampaign.Area), WorkloadName),
        IReactorHandler<AccessReviewCampaignCompletionCanceled>
{
    public const string WorkloadName = "AccessReviewCompletionSettlementV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewCampaignCompletionCanceled> context,
        CancellationToken ct)
    {
        await AccessReviewReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        var tenant = Uuid.Parse(context.Source.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var campaignId = Uuid.Parse(context.Source.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture);
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenant, campaignId, selectedRootId: context.Trigger.Operation.RootOperationId), ct).ConfigureAwait(false);
        var accepted = campaign.AcceptedFor(context.Trigger.Operation.RootOperationId);
        if (accepted is null)
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                "The accepted completion root is not visible yet.", true));
        var key = ProcessKey.ForRoot(AccessReviewEffects.CompletionProcessName,
            context.Trigger.Operation.RootOperationId, NativeFactReference.Of(context.Source.Stream, accepted));
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
