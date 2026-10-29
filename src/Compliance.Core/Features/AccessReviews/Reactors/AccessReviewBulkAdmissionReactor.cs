using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Reactors;

/// <summary>
///     Starts <c>BulkAccessDecisionProcess</c> from a committed bulk decision admission. The process record, with the
///     complete canonical target list of the frozen selection, exists before the source checkpoint advances; the sweep
///     resumes it if this reactor stops. A selection that cannot be read yet is transient: no record is created from an
///     unverified admission.
/// </summary>
sealed partial class AccessReviewBulkAdmissionReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content, AccessReviewManifests manifests, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewCampaign.Area), WorkloadName),
        IReactorHandler<AccessReviewDecisionBatchAdmitted>
{
    public const string WorkloadName = "AccessReviewDecisionBatchAdmissionV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewDecisionBatchAdmitted> context, CancellationToken ct)
    {
        await AccessReviewReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        var realm = context.Source.Stream.Realm;
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var campaignId = Uuid.Parse(context.Source.Stream.Resource, CultureInfo.InvariantCulture);
        IReadOnlyList<string> targets;
        try
        {
            var intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenant,
                context.Trigger.Intent, ct).ConfigureAwait(false);
            var items = await manifests.BulkItemsAsync(realm, intent.Items, ct).ConfigureAwait(false);
            if (items.Count != intent.Items.Count)
                throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                    "The frozen selection is not duplicate-free.", false));
            targets = [.. items.Values.Select(item => AccessReviewCompletionTarget.Of(tenant, campaignId, item.ItemId)
                .ToString())];
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen bulk decision cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(AccessReviewEffects.BulkProcessName, context, context.Trigger.Intent, targets,
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
