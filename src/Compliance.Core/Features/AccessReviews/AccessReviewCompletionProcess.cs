using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The ordered access review completion process: acquire the exact pinned head of every item in canonical key
///     order, close the campaign, then settle every item use (which installs the closure fence). The abort receiver
///     cancels a completion that can never close and fences every delayed finalization of its generation before any
///     item use is settled.
/// </summary>
static class AccessReviewCompletionProcess
{
    public const string Acquire = "acquire";
    public const string Complete = "complete";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(AccessReviewEffects.CompletionProcessName,
        [nameof(AccessReviewCampaignCompletionAccepted)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AuthorizeAccessReviewItemCampaignCompletionUse)],
                "Canonical order over the drained item set."),
            new ProcessStageDefinition(Complete, [nameof(CompleteAccessReviewCampaignClosure)]),
            new ProcessStageDefinition(Settle, [nameof(SettleAccessReviewItemCampaignCompletionUse)],
                "Installs the parent-generation closure fence."),
        ], [nameof(AbortAccessReviewCampaignCompletion)], Receiver);

    /// <summary>The command id the process driver derives for a receiver step of the completion.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        AccessReviewEffects.ReceiverCommandId(AccessReviewEffects.CompletionProcessName, rootSourceEventId, receiver,
            target, episode);

    /// <summary>The settle receiver of one item with the disposition its terminal campaign fact requires.</summary>
    public static SettleAccessReviewItemCampaignCompletionUse SettleCommand(ProcessRecord record, string target,
        Uuid commandId, SourceUseSettlementDisposition disposition)
    {
        var tenant = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        var campaignId = Uuid.Parse(record.Source.Stream.Resource, CultureInfo.InvariantCulture);
        var item = AccessReviewCompletionTarget.Parse(target);
        return new SettleAccessReviewItemCampaignCompletionUse(commandId, record.Source,
            AccessReviewEffects.ItemStream(tenant, campaignId, item.ItemId), record.Key.RootOperationId,
            AccessReviewEffects.Generation, null, record.Intent, item.ItemId, campaignId, disposition);
    }

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var realm = record.Key.Realm;
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var campaignId = Uuid.Parse(record.Source.Stream.Resource, CultureInfo.InvariantCulture);
        var root = record.Key.RootOperationId;
        var content = services.GetRequiredService<IImmutableContentStore>();
        switch (step.Receiver)
        {
            case nameof(SettleAccessReviewItemCampaignCompletionUse):
                return SettleCommand(record, step.Target!, step.CommandId, record.State == ProcessState.Aborting
                    ? SourceUseSettlementDisposition.Canceled
                    : SourceUseSettlementDisposition.ParentClosed);
            case nameof(AuthorizeAccessReviewItemCampaignCompletionUse):
                {
                    var target = AccessReviewCompletionTarget.Parse(step.Target!);
                    var intent = await ReadIntentAsync(content, tenant, record, ct).ConfigureAwait(false);
                    SourceUseRequirement? requirement;
                    try
                    {
                        requirement = (await services.GetRequiredService<AccessReviewManifests>()
                            .RequirementsAsync(realm, intent.SourceUses, ct).ConfigureAwait(false))
                            .GetValueOrDefault(target.Resource);
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        throw new SourceFactVerificationException(
                            $"The frozen completion cannot be verified yet: {exception.Message}");
                    }

                    if (requirement is null)
                        throw new InvalidOperationException($"{step.Target} is not pinned by the process record.");
                    return new AuthorizeAccessReviewItemCampaignCompletionUse(step.CommandId, record.Source,
                        AccessReviewEffects.ItemStream(tenant, campaignId, target.ItemId), root,
                        AccessReviewEffects.Generation, null, record.Intent, target.ItemId,
                        requirement.ContentEvent.EventId, checked((long)requirement.ResourceRevision), campaignId);
                }
            case nameof(CompleteAccessReviewCampaignClosure):
                {
                    var intent = await ReadIntentAsync(content, tenant, record, ct).ConfigureAwait(false);
                    return new CompleteAccessReviewCampaignClosure(step.CommandId, record.Source,
                        AccessReviewEffects.CampaignStream(tenant, campaignId), root, AccessReviewEffects.Generation, null,
                        record.Intent, campaignId, intent.SourceUses.Content);
                }
            case nameof(AbortAccessReviewCampaignCompletion):
                {
                    var campaign = await services.GetRequiredService<IAggregateReader>()
                        .HydrateAsync(new AccessReviewCampaign(tenant, campaignId), ct).ConfigureAwait(false);
                    if (campaign.AcceptedFor(root) is not { } accepted)
                        throw new SourceFactVerificationException("The accepted completion is not visible yet.");
                    var proof = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(step.CommandId, "failure-proof"),
                        AccessReviewContent.FailureProofSchema, new AccessReviewCompletionFailure(
                            "The completion process was aborted.", null, NativeFactReference.Of(campaign.Stream, accepted)),
                        ct).ConfigureAwait(false);
                    return new AbortAccessReviewCampaignCompletion(step.CommandId, root, campaign.Stream,
                        AccessReviewEffects.Generation, accepted.Revision, proof);
                }
            default:
                throw new InvalidOperationException($"{step.Receiver} is not an access review completion receiver.");
        }
    }

    static async Task<ClosureIntent> ReadIntentAsync(IImmutableContentStore content, Uuid tenant, ProcessRecord record,
        CancellationToken ct)
    {
        try
        {
            return await AccessReviewContent.ReadAsync<ClosureIntent>(content, tenant, record.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen completion cannot be verified yet: {exception.Message}");
        }
    }
}
