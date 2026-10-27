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
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessReviews.Reactors;

/// <summary>
///     Starts <c>AccessReviewLaunchFanOut</c> from a committed launch. The process record, with the complete canonical
///     item list of the launch's intent manifest, exists before the source checkpoint advances; the sweep resumes it
///     if this reactor stops. A manifest that cannot be read yet is transient: no record is created from an
///     unverified launch.
/// </summary>
sealed partial class AccessReviewLaunchFanOutReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AccessReviewCampaign.Area), WorkloadName),
        IReactorHandler<AccessReviewCampaignLaunched>
{
    public const string WorkloadName = "AccessReviewLaunchFanOutV1";

    public async ValueTask HandleAsync(IReactorContext<AccessReviewCampaignLaunched> context, CancellationToken ct)
    {
        await AccessReviewReactorSources.VerifyAsync(sources, content, Pattern, context, context.Trigger.Operation.Owner, ct)
            .ConfigureAwait(false);
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
                $"The launch manifest cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(AccessReviewEffects.LaunchFanOutName, context, launched.ItemIntentManifest,
            manifest.Selections.Select(static selection => selection.ItemId.ToString()), $"reactor:{WorkloadName}", ct)
            .ConfigureAwait(false);
    }
}
