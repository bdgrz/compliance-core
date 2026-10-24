using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Starts <c>BulkAccessDecisionProcess</c> from a committed bulk decision admission. The process record, with the
///     complete canonical target list of the frozen selection, exists before the source checkpoint advances; the sweep
///     resumes it if this reactor stops. A selection that cannot be read yet is transient: no record is created from an
///     unverified admission.
/// </summary>
sealed partial class AccessReviewBulkAdmissionReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content, AccessReviewManifests manifests)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewCampaign.Area), WorkloadName),
        IReactorHandler<AccessReviewDecisionBatchAdmitted>
{
    public const string WorkloadName = "AccessReviewDecisionBatchAdmissionV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewDecisionBatchAdmitted> context, CancellationToken ct)
    {
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
                $"The frozen bulk decision cannot be verified yet: {exception.Message}", true));
        }

        _ = await process.StartAsync(AccessReviewEffects.BulkProcessName, context, context.Trigger.Intent, targets,
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
