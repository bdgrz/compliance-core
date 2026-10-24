using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

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

        AccessReviewItemCreationIntent intent;
        AccessReviewItemView entry;
        try
        {
            intent = await AccessReviewContent.ReadAsync<AccessReviewItemCreationIntent>(content, tenant, command.Intent,
                ct).ConfigureAwait(false);
            entry = await AccessReviewContent.ReadAsync<AccessReviewItemView>(content, tenant, intent.PopulationEntry, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen creation intent cannot be verified: {exception.Message}");
        }

        if (intent.CampaignId != command.CampaignId || intent.ItemId != command.ItemId ||
            intent.Header.RootOperationId != command.OperationId ||
            intent.CampaignGeneration != (ulong)AccessReviewEffects.Generation || entry.ItemId != command.ItemId ||
            entry.SystemInstanceId != intent.SystemInstanceId || entry.ReviewerMemberId != intent.ReviewerMemberId)
            return SourceUseReceiverSupport.Permanent("The materialization differs from the frozen creation intent.");

        var source = await VerifySourceAsync(tenant, command, ct).ConfigureAwait(false);
        if (source is { } rejected)
            return rejected;

        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(MaterializeAccessReviewItem),
            command.Source.EventId, intent.Header, target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new AccessReviewItem(tenant, command.CampaignId, command.ItemId,
                    command.CommandId), ct).ConfigureAwait(false);
                if (Decide(current, binding) is { } early)
                    return early;
                if (current.Opened is { } existing)
                    return existing.Intent == command.Intent
                        ? SourceUseReceiverSupport.Replayed(Fact(current.Stream, existing))
                        : SourceUseReceiverSupport.Permanent(
                            "The item is already materialized with a different frozen creation intent.");

                var step = await executor.ExecuteAsync(new AccessReviewItem(tenant, command.CampaignId, command.ItemId,
                    command.CommandId), item =>
                    {
                        if (Decide(item, binding) is { } decided)
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

    /// <summary>The launch fact that carries exactly this item and intent must be found on the authoritative campaign.</summary>
    async ValueTask<Result<ReceiverOutcome>?> VerifySourceAsync(Uuid tenant, MaterializeAccessReviewItem command,
        CancellationToken ct)
    {
        var source = command.Source;
        var stream = AccessReviewEffects.CampaignStream(tenant, command.CampaignId);
        if (source.Stream != stream)
            return SourceUseReceiverSupport.Permanent("The source is not a launch of this campaign.");
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenant, command.CampaignId), ct)
            .ConfigureAwait(false);
        if (campaign.Launched is not { } launched || launched.Metadata!.EventId != source.EventId)
            return SourceUseReceiverSupport.Transient("The launch is not visible on its campaign yet.");
        if (launched.Metadata.AggregateVersion != source.AggregateVersion ||
            launched.Operation.RootOperationId != command.OperationId)
            return SourceUseReceiverSupport.Permanent("The source differs from the launch of this operation.");
        try
        {
            var intents = await manifests.IntentsAsync(command.Target.Realm, launched.ItemIntentManifest, ct)
                .ConfigureAwait(false);
            return intents.TryGetValue(command.ItemId, out var frozen) && frozen == command.Intent
                ? null
                : SourceUseReceiverSupport.Permanent("The launch manifest does not carry this item and intent.");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Transient($"The launch manifest cannot be verified yet: {exception.Message}");
        }
    }

    static Result<ReceiverOutcome>? Decide(AccessReviewItem item, AcceptedOperationBinding binding) =>
        item.Acceptance.Decide(binding) switch
        {
            NativeAcceptanceDecision.Replayed => SourceUseReceiverSupport.Replayed(Fact(item.Stream,
                item.Acceptance.SelectedFact!)),
            NativeAcceptanceDecision.Conflict => Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError),
            _ => null,
        };

    static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) => NativeFactReference.Of(stream, fact);

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
