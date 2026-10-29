using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Completes the accepted cancellation once every selected item carries its fence. The fence set the command
///     presents is only a claim: the receiver hydrates every item and finds its exact fence fact, so a cancellation
///     cannot complete while a delayed hold or apply could still land. A fence that is not visible yet is transient and
///     a mismatch is permanent. A batch that already published is a terminal no-op.
/// </summary>
sealed class CompleteAccessReviewDecisionBatchCancellationHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AccessReviewBulkVerifier verifier, AccessReviewManifests manifests,
    TimeProvider clock) : IRequestHandler<CompleteAccessReviewDecisionBatchCancellation, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteAccessReviewDecisionBatchCancellation> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.DecisionBatchId != command.OperationId ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            !Uuid.TryParse(command.Source.Stream.Resource, CultureInfo.InvariantCulture, out var campaignId) ||
            command.Target != AccessReviewEffects.BatchStream(tenant, campaignId, command.DecisionBatchId))
            return SourceUseReceiverSupport.Permanent(
                "The completion names another batch stream, operation or generation.");
        var bulk = await verifier.ReadNativeAsync(tenant, campaignId, command.Source, command.Intent, command.OperationId, ct)
            .ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<ReceiverOutcome>.Failure(bulk.Error);
        var provenance = SourceUseReceiverSupport.Child(command.CommandId,
            nameof(CompleteAccessReviewDecisionBatchCancellation), command.Source.EventId,
            AccessReviewIntentBindings.HeaderOf(bulk.Admitted!.Operation), command.Target,
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<AccessReviewDecisionBatch, VerifiedCancellation>(reader,
            () => new AccessReviewDecisionBatch(tenant, campaignId, command.DecisionBatchId, command.CommandId),
            owner => Decide(owner, binding, command, provenance),
            (owner, token) => PrepareAsync(tenant, campaignId, command, bulk, owner, token), ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new AccessReviewDecisionBatch(tenant, campaignId,
                    command.DecisionBatchId, command.CommandId), fresh =>
                    {
                        if (Decide(fresh, binding, command, provenance) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CompleteCancellation(provenance, command.ItemFenceProofs);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(BulkReceiverSupport.Fact(fresh.Stream,
                                    fresh.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The batch changed concurrently; rehydrate and decide again.
            }
        }
    }

    async ValueTask<Result<VerifiedCancellation>> PrepareAsync(Uuid tenant, Uuid campaignId,
        CompleteAccessReviewDecisionBatchCancellation command, AccessReviewBulk native, AccessReviewDecisionBatch batch,
        CancellationToken ct)
    {
        var bulk = await verifier.PrepareAsync(tenant, native, ct).ConfigureAwait(false);
        if (bulk.Error is not null)
            return Result<VerifiedCancellation>.Failure(bulk.Error);
        if (batch.CancellationAccepted is null)
            return Result<VerifiedCancellation>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The batch cancellation is not visible yet.", true));
        try
        {
            if (await VerifyFencesAsync(tenant, campaignId, command, bulk.Intent!, ct).ConfigureAwait(false) is { } failure)
                return Result<VerifiedCancellation>.Failure(failure.Error!);
            return Result<VerifiedCancellation>.Success(new VerifiedCancellation());
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<VerifiedCancellation>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen bulk decision cannot be verified yet: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async Task<Result<ReceiverOutcome>?> VerifyFencesAsync(Uuid tenant, Uuid campaignId,
        CompleteAccessReviewDecisionBatchCancellation command, BulkAccessDecisionIntent intent, CancellationToken ct)
    {
        var proofs = await AccessReviewContent.ReadAsync<IReadOnlyList<BulkItemFenceProof>>(content, tenant,
            command.ItemFenceProofs, ct).ConfigureAwait(false);
        var items = await manifests.BulkItemsAsync(command.Target.Realm, intent.Items, ct).ConfigureAwait(false);
        if (proofs.Count != items.Count)
            return SourceUseReceiverSupport.Permanent("The fence set is not the complete selection.");
        if (proofs.Select(static proof => proof.ItemId).Distinct().Count() != proofs.Count)
            return SourceUseReceiverSupport.Permanent("The fence set repeats an item.");
        var byItem = proofs.ToDictionary(static proof => proof.ItemId);
        foreach (var selected in items.Values)
        {
            if (!byItem.TryGetValue(selected.ItemId, out var proof))
                return SourceUseReceiverSupport.Permanent("The fence set omits a selected item.");
            var item = await reader.HydrateAsync(new AccessReviewItem(tenant, campaignId, selected.ItemId, selectedBatchId: intent.DecisionBatchId), ct)
                .ConfigureAwait(false);
            if (item.FenceFor(command.DecisionBatchId) is not { } fence)
                return SourceUseReceiverSupport.Transient($"The fence of item {selected.ItemId} is not visible yet.");
            if (proof.Fence != BulkReceiverSupport.Fact(item.Stream, fence))
                return SourceUseReceiverSupport.Permanent($"The fence proof of item {selected.ItemId} differs from its fence.");
        }

        return null;
    }

    static Result<ReceiverOutcome>? Decide(AccessReviewDecisionBatch batch, AcceptedOperationBinding binding,
        CompleteAccessReviewDecisionBatchCancellation command, OperationProvenance provenance)
    {
        if (batch.Accepted is { } materialized &&
            (materialized.Intent != command.Intent || materialized.Operation.RootOperationId != command.OperationId ||
                materialized.Operation.CausationEventId != command.Source.EventId || materialized.Operation.FirstOwner != provenance.FirstOwner))
            return SourceUseReceiverSupport.Permanent("The materialized batch has another exact native admission.");
        if (batch.Canceled is { } prior &&
            (prior.Intent != command.Intent || prior.Operation.RootOperationId != command.OperationId ||
                prior.Operation.CausationEventId != command.Source.EventId || prior.Operation.FirstOwner != provenance.FirstOwner ||
                AcceptedOperationBinding.Of(prior.Operation with { LogicalId = provenance.LogicalId }) != binding))
            return SourceUseReceiverSupport.Permanent("The canceled batch has another exact native source binding.");
        // As before, an accepted cancellation retry returns its recorded fence set; only a new one verifies the claim.
        if (BulkReceiverSupport.Decide(batch.Acceptance, batch.Stream, binding) is { } accepted)
            return accepted;
        if (batch.Canceled is { } canceled)
            return SourceUseReceiverSupport.Replayed(BulkReceiverSupport.Fact(batch.Stream, canceled));
        return batch.Published is { } published
            ? SourceUseReceiverSupport.Superseded("The batch already published; its decisions recover forward.",
                BulkReceiverSupport.Fact(batch.Stream, published))
            : null;
    }

    sealed record VerifiedCancellation;

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
