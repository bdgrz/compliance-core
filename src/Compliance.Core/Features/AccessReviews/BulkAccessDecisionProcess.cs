using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The ordered bulk decision process: materialize the batch from the admitted intent, hold every selected item at
///     the head the preview froze in canonical key order before any apply, apply the shared decision provisionally to
///     every item, publish all of them as one fact and settle the holds (which makes the decisions visible). The abort
///     receiver cancels a batch that can never publish; the item fences, the cancellation completion and the settlement
///     of the holds then follow from the cancellation facts.
/// </summary>
static class BulkAccessDecisionProcess
{
    public const string Materialize = "materialize";
    public const string Reserve = "reserve";
    public const string Apply = "apply";
    public const string Publish = "publish";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(AccessReviewEffects.BulkProcessName,
        [nameof(AccessReviewDecisionBatchAdmitted)], true,
        [
            new ProcessStageDefinition(Materialize, [nameof(MaterializeAccessReviewDecisionBatch)], null, false),
            new ProcessStageDefinition(Reserve, [nameof(ReserveAccessReviewItemDecision)],
                "All selected items in canonical order before any apply."),
            new ProcessStageDefinition(Apply, [nameof(ApplyAccessReviewItemDecision)],
                "All items; provisional and hidden.", true),
            new ProcessStageDefinition(Publish, [nameof(PublishAccessReviewDecisionBatch)], "One publication fact."),
            new ProcessStageDefinition(Settle, [nameof(SettleAccessReviewItemDecision)]),
        ], [nameof(CancelAccessReviewDecisionBatch)], Receiver);

    /// <summary>The command id the process driver derives for a receiver step of the batch.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        AccessReviewEffects.ReceiverCommandId(AccessReviewEffects.BulkProcessName, rootSourceEventId, receiver, target,
            episode);

    /// <summary>The settle receiver of one item with the disposition the batch's terminal fact requires.</summary>
    public static SettleAccessReviewItemDecision SettleCommand(NativeFactReference source, ImmutableContentRef intent,
        Uuid tenant, Uuid campaignId, Uuid decisionBatchId, Uuid itemId, Uuid commandId,
        SourceUseSettlementDisposition disposition) => new(commandId, source,
        AccessReviewEffects.ItemStream(tenant, campaignId, itemId), decisionBatchId, AccessReviewEffects.Generation,
        null, intent, itemId, campaignId, decisionBatchId, disposition);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var realm = record.Key.Realm;
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var campaignId = Uuid.Parse(record.Source.Stream.Resource, CultureInfo.InvariantCulture);
        var batchId = record.Key.RootOperationId;
        var content = services.GetRequiredService<IImmutableContentStore>();
        switch (step.Receiver)
        {
            case nameof(MaterializeAccessReviewDecisionBatch):
                return new MaterializeAccessReviewDecisionBatch(step.CommandId, record.Source,
                    AccessReviewEffects.BatchStream(tenant, campaignId, batchId), batchId, AccessReviewEffects.Generation,
                    null, record.Intent, campaignId, batchId);
            case nameof(ReserveAccessReviewItemDecision):
                {
                    var target = AccessReviewCompletionTarget.Parse(step.Target!);
                    var (selected, head) = await ItemOfAsync(services, tenant, record, target, ct).ConfigureAwait(false);
                    return new ReserveAccessReviewItemDecision(step.CommandId, record.Source,
                        AccessReviewEffects.ItemStream(tenant, campaignId, target.ItemId), batchId,
                        AccessReviewEffects.Generation, null, record.Intent, target.ItemId, head.EventId,
                        selected.ExpectedItemRevision, campaignId, batchId);
                }
            case nameof(ApplyAccessReviewItemDecision):
                {
                    var target = AccessReviewCompletionTarget.Parse(step.Target!);
                    return new ApplyAccessReviewItemDecision(step.CommandId, record.Source,
                        AccessReviewEffects.ItemStream(tenant, campaignId, target.ItemId), batchId,
                        AccessReviewEffects.Generation, null, record.Intent, campaignId, target.ItemId, batchId);
                }
            case nameof(PublishAccessReviewDecisionBatch):
                {
                    var proofs = await ProofsAsync(services, tenant, campaignId, batchId, record, step, ct)
                        .ConfigureAwait(false);
                    return new PublishAccessReviewDecisionBatch(step.CommandId, record.Source,
                        AccessReviewEffects.BatchStream(tenant, campaignId, batchId), batchId,
                        AccessReviewEffects.Generation, null, record.Intent, batchId, proofs);
                }
            case nameof(SettleAccessReviewItemDecision):
                {
                    var target = AccessReviewCompletionTarget.Parse(step.Target!);
                    return SettleCommand(record.Source, record.Intent, tenant, campaignId, batchId, target.ItemId,
                        step.CommandId, record.State == ProcessState.Aborting
                            ? SourceUseSettlementDisposition.Canceled
                            : SourceUseSettlementDisposition.IrreversibleDecision);
                }
            case nameof(CancelAccessReviewDecisionBatch):
                {
                    var proof = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(step.CommandId, "failure-proof"),
                        AccessReviewContent.BulkFailureProofSchema, new AccessReviewBulkFailure(
                            "The bulk decision process was aborted.", null, record.Source), ct).ConfigureAwait(false);
                    return new CancelAccessReviewDecisionBatch(step.CommandId,
                        AccessReviewEffects.BatchStream(tenant, campaignId, batchId), campaignId, batchId,
                        AccessReviewEffects.Generation, 0, proof);
                }
            default:
                throw new InvalidOperationException($"{step.Receiver} is not a bulk decision receiver.");
        }
    }

    static async Task<(BulkAccessDecisionItemIntent Selected, NativeFactReference Head)> ItemOfAsync(
        IServiceProvider services, Uuid tenant, ProcessRecord record, AccessReviewCompletionTarget target,
        CancellationToken ct)
    {
        var content = services.GetRequiredService<IImmutableContentStore>();
        try
        {
            var intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenant, record.Intent, ct)
                .ConfigureAwait(false);
            var verifier = services.GetRequiredService<AccessReviewBulkVerifier>();
            var selected = await verifier.ItemOfAsync(record.Key.Realm, intent, target.Resource, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{target} is not selected by the process record.");
            return (selected, await verifier.HeadOfAsync(tenant, selected, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen bulk decision cannot be verified yet: {exception.Message}");
        }
    }

    /// <summary>The complete publication proofs: every target's exact hold and provisional decision, or not provable yet.</summary>
    static async Task<ImmutableContentRef> ProofsAsync(IServiceProvider services, Uuid tenant, Uuid campaignId,
        Uuid batchId, ProcessRecord record, ProcessStep step, CancellationToken ct)
    {
        var reader = services.GetRequiredService<IAggregateReader>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var proofs = new List<BulkItemDecisionProof>(record.Targets.Count);
        foreach (var targetKey in record.Targets)
        {
            var target = AccessReviewCompletionTarget.Parse(targetKey);
            var item = await reader.HydrateAsync(new AccessReviewItem(tenant, campaignId, target.ItemId).ForSourceUse(candidate => candidate.BulkKey(batchId)), ct)
                .ConfigureAwait(false);
            var key = item.BulkKey(batchId);
            if (item.ProvisionalFor(batchId) is not { } provisional || item.Uses.GrantOf(key) is not { } hold)
                throw new SourceFactUnavailableException(
                    $"The hold and provisional decision of item {target.ItemId} are not visible yet.");
            proofs.Add(new BulkItemDecisionProof(target.ItemId, hold.GrantEvent,
                NativeFactReference.Of(item.Stream, provisional)));
        }

        return await content.WriteJsonAsync<IReadOnlyList<BulkItemDecisionProof>>(record.Key.Realm,
            Uuid.CreateVersion5(step.CommandId, "item-decision-proofs"), BulkItemDecisionProof.Schema, proofs, ct)
            .ConfigureAwait(false);
    }
}
