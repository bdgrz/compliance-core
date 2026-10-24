using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Starts <c>AccessReviewLaunchFanOut</c> from a committed launch. The process record, with the complete canonical
///     item list of the launch's intent manifest, exists before the source checkpoint advances; the sweep resumes it
///     if this reactor stops. A manifest that cannot be read yet is transient: no record is created from an
///     unverified launch.
/// </summary>
sealed partial class AccessReviewLaunchFanOutReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewCampaign.Area), WorkloadName),
        IReactorHandler<AccessReviewCampaignLaunched>
{
    public const string WorkloadName = "AccessReviewLaunchFanOutV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewCampaignLaunched> context, CancellationToken ct)
    {
        var launched = context.Trigger;
        AccessReviewItemIntentManifest manifest;
        try
        {
            manifest = await content.ReadJsonAsync<AccessReviewItemIntentManifest>(context.Source.Stream.Realm,
                launched.ItemIntentManifest, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The launch manifest cannot be verified yet: {exception.Message}", true));
        }

        _ = await process.StartAsync(AccessReviewEffects.LaunchFanOutName, context, launched.ItemIntentManifest,
            manifest.Selections.Select(static selection => selection.ItemId.ToString()), $"reactor:{WorkloadName}", ct)
            .ConfigureAwait(false);
    }
}
