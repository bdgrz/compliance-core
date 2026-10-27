using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Materializes one frozen item. The frozen creation intent is read from verified content and the launch fact that
///     carries exactly that intent is found on the authoritative campaign; the item view the intent names is verified
///     against it. Nothing a command states is trusted until its owner confirms it. An exact replay returns the
///     original fact; an item materialized by another command with the same intent is the same item; a different
///     intent for one item is a permanent conflict. A fact that is not visible yet is transient.
/// </summary>
sealed class MaterializeAccessReviewItemHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AccessReviewManifests manifests, TimeProvider clock)
    : IRequestHandler<MaterializeAccessReviewItem, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<MaterializeAccessReviewItem> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.ExpectedRevision is not (null or 0) ||
            command.CampaignId == Uuid.Empty || command.ItemId == Uuid.Empty ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The materialization names another generation, operation or realm.");
        var target = AccessReviewEffects.ItemStream(tenant, command.CampaignId, command.ItemId);
        if (command.Target != target)
            return SourceUseReceiverSupport.Permanent("The materialization names another item stream.");

        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenant, command.CampaignId), ct).ConfigureAwait(false);
        var launched = campaign.Launched;
        if (command.Source.Stream != campaign.Stream)
            return SourceUseReceiverSupport.Permanent("The source is not a launch of this campaign.");
        if (launched is null || launched.Metadata.EventId != command.Source.EventId)
            return SourceUseReceiverSupport.Transient("The launch is not visible on its campaign yet.");
        if (launched.Metadata.AggregateVersion != command.Source.AggregateVersion ||
            launched.Operation.RootOperationId != command.OperationId)
            return SourceUseReceiverSupport.Permanent("The source differs from the launch of this operation.");
        var operation = launched.Operation;
        var header = AccessReviewIntentBindings.HeaderOf(operation);
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(MaterializeAccessReviewItem),
            command.Source.EventId, header, target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<AccessReviewItem, AccessReviewItemCreationIntent>(reader,
            () => new AccessReviewItem(tenant, command.CampaignId, command.ItemId, command.CommandId),
            owner => Decide(owner, binding, command, provenance), (_, token) => PrepareAsync(tenant, command, launched, token), ct)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new AccessReviewItem(tenant, command.CampaignId, command.ItemId,
                    command.CommandId), ct).ConfigureAwait(false);
                if (Decide(current, binding, command, provenance) is { } early)
                    return early;

                var step = await executor.ExecuteAsync(new AccessReviewItem(tenant, command.CampaignId, command.ItemId,
                    command.CommandId), item =>
                    {
                        if (Decide(item, binding, command, provenance) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        if (item.Opened is not null)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Transient(
                                    "The item was materialized concurrently; the replay is decided again."))));
                        var raised = item.Materialize(provenance, command.Intent);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(Fact(item.Stream, item.Opened!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The item changed concurrently; rehydrate and decide again.
            }
        }
    }

    async ValueTask<Result<AccessReviewItemCreationIntent>> PrepareAsync(Uuid tenant, MaterializeAccessReviewItem command,
        AccessReviewCampaignLaunched launched, CancellationToken ct)
    {
        try
        {
            var intent = await AccessReviewContent.ReadAsync<AccessReviewItemCreationIntent>(content, tenant, command.Intent, ct)
                .ConfigureAwait(false);
            var entry = await AccessReviewContent.ReadAsync<AccessReviewItemView>(content, tenant, intent.PopulationEntry, ct)
                .ConfigureAwait(false);
            if (intent.Header is null || intent.CampaignId != command.CampaignId || intent.ItemId != command.ItemId ||
                intent.Header.RootOperationId != command.OperationId ||
                intent.CampaignGeneration != (ulong)AccessReviewEffects.Generation || entry.ItemId != command.ItemId ||
                entry.SystemInstanceId != intent.SystemInstanceId || entry.ReviewerMemberId != intent.ReviewerMemberId)
                return Failed("The materialization differs from the frozen creation intent.");
            AccessReviewIntentBindings.Require(intent.Header, launched.Operation);
            var intents = await manifests.IntentsAsync(command.Target.Realm, launched.ItemIntentManifest, ct).ConfigureAwait(false);
            return intents.TryGetValue(command.ItemId, out var frozen) && frozen == command.Intent
                ? Result<AccessReviewItemCreationIntent>.Success(intent)
                : Failed("The launch manifest does not carry this item and intent.");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<AccessReviewItemCreationIntent>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The frozen creation intent cannot be verified: " + exception.Message,
                exception is ImmutableContentUnavailableException));
        }
    }

    static Result<AccessReviewItemCreationIntent> Failed(string message) =>
        Result<AccessReviewItemCreationIntent>.Failure(new RequestError(RequestErrorKind.Conflict, message));

    static Result<ReceiverOutcome>? Decide(AccessReviewItem item, AcceptedOperationBinding binding,
        MaterializeAccessReviewItem command, OperationProvenance provenance)
    {
        if (item.Opened is { } opened &&
            (opened.Intent != command.Intent || opened.Operation.RootOperationId != command.OperationId ||
                opened.Operation.CausationEventId != command.Source.EventId || opened.Operation.FirstOwner != provenance.FirstOwner))
            return SourceUseReceiverSupport.Permanent("The item is already materialized with another exact source or frozen intent.");
        switch (item.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(Fact(item.Stream, item.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }
        if (item.Opened is not { } existing)
            return null;
        return AcceptedOperationBinding.Of(existing.Operation with { LogicalId = provenance.LogicalId }) == binding
            ? SourceUseReceiverSupport.Replayed(Fact(item.Stream, existing))
            : SourceUseReceiverSupport.Permanent("The existing item has another exact native source binding.");
    }

    static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) => NativeFactReference.Of(stream, fact);

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
