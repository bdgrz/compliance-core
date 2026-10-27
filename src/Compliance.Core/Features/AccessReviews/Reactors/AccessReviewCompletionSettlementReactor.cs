using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
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
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
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
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Reactors;

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
