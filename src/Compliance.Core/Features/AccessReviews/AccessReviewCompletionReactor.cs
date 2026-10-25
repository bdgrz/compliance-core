using Bdgrz.Compliance.Features.SourceFacts;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Starts <c>AccessReviewCampaignCompletionProcess</c> from a committed completion admission. The process record,
///     with the complete canonical target list of the drained item set, exists before the source checkpoint advances;
///     the sweep resumes it if this reactor stops. A set that cannot be read yet is transient.
/// </summary>
sealed partial class AccessReviewCompletionReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content, IAggregateReader reader, AccessReviewManifests manifests, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewCampaign.Area), WorkloadName),
        IReactorHandler<AccessReviewCampaignCompletionAccepted>
{
    public const string WorkloadName = "AccessReviewCampaignCompletionV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewCampaignCompletionAccepted> context,
        CancellationToken ct)
    {
        await AccessReviewReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
        var realm = context.Source.Stream.Realm;
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var campaignId = Uuid.Parse(context.Source.Stream.Resource, CultureInfo.InvariantCulture);
        IReadOnlyList<string> targets;
        try
        {
            var intent = await AccessReviewContent.ReadAsync<ClosureIntent>(content, tenant, context.Trigger.Intent, ct)
                .ConfigureAwait(false);
            var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenant, campaignId), ct)
                .ConfigureAwait(false);
            if (campaign.Launched is not { } launched)
                throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                    "The launch of the completing campaign is not visible yet.", true));
            targets = await manifests.CompletionTargetsAsync(tenant, campaignId, launched, intent, ct)
                .ConfigureAwait(false);
            if (targets.Count != intent.SourceUses.Count)
                throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                    "The pinned item set differs from the launched items.", false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen completion cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(AccessReviewEffects.CompletionProcessName, context, context.Trigger.Intent, targets,
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
