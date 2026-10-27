using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The unordered access review launch fan-out. The root is a campaign's <see cref="AccessReviewCampaignLaunched" />
///     fact; its targets are the item ids of the launch's complete intent manifest, one
///     <see cref="MaterializeAccessReviewItem" /> receiver per target in canonical key order. No receiver depends on
///     another, and the deterministic item identity makes a crash, a replay or a resumed run converge on one item.
/// </summary>
static class AccessReviewLaunchFanOut
{
    public const string Materialize = "materialize";

    public static ProcessDefinition Definition() => new(AccessReviewEffects.LaunchFanOutName,
        [nameof(AccessReviewCampaignLaunched)], false,
        [
            new ProcessStageDefinition(Materialize, [nameof(MaterializeAccessReviewItem)],
                "One receiver command per item in the launch manifest; items are independent."),
        ], [], Receiver);

    /// <summary>The command id the process driver derives for the receiver of one item of the launch.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string target, string episode) =>
        AccessReviewEffects.ReceiverCommandId(AccessReviewEffects.LaunchFanOutName, rootSourceEventId,
            nameof(MaterializeAccessReviewItem), target, episode);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        if (step.Target is null)
            return null;
        var record = step.Record;
        var realm = record.Key.Realm;
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var campaignId = Uuid.Parse(record.Source.Stream.Resource, CultureInfo.InvariantCulture);
        var itemId = Uuid.Parse(step.Target, CultureInfo.InvariantCulture);
        ImmutableContentRef intent;
        try
        {
            var intents = await services.GetRequiredService<AccessReviewManifests>().IntentsAsync(realm, record.Intent, ct)
                .ConfigureAwait(false);
            if (!intents.TryGetValue(itemId, out intent!))
                throw new InvalidOperationException($"{step.Target} is not selected by the process record.");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen launch cannot be verified yet: {exception.Message}");
        }

        return new MaterializeAccessReviewItem(step.CommandId, record.Source,
            AccessReviewEffects.ItemStream(tenant, campaignId, itemId), record.Key.RootOperationId,
            AccessReviewEffects.Generation, null, intent, campaignId, itemId);
    }
}
