using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
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
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
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
