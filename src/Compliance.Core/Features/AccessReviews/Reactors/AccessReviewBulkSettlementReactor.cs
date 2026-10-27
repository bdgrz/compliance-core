using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Reactors;

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
