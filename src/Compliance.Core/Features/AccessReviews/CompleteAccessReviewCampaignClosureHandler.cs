using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Closes the campaign after every item was held. Under the holds it verifies that every frozen item exists and
///     that its grant is the exact one the completion pinned, re-reads each item's decision, remediation and
///     exception proof (an exception is judged at close time), freezes the final campaign snapshot and appends the
///     completion with the complete proof set. A permanent proof mismatch cancels the completion and fences it; a
///     fact that is not visible yet is transient and a closure that already happened replays.
/// </summary>
sealed class CompleteAccessReviewCampaignClosureHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AccessReviewManifests manifests, PopulationSnapshotFreezer freezer,
    AccessReviewCompletionVerifier verifier, AccessReviewCompletionFences fences, TimeProvider clock)
    : IRequestHandler<CompleteAccessReviewCampaignClosure, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteAccessReviewCampaignClosure> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.CampaignId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation || command.ExpectedRevision is not null ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            command.Target != AccessReviewEffects.CampaignStream(tenant, command.CampaignId))
            return SourceUseReceiverSupport.Permanent("The closure names another campaign stream, operation or generation.");
        var completion = await verifier.VerifyAsync(tenant, command.CampaignId, command.Source, command.Intent,
            command.OperationId, command.CommandId, ct).ConfigureAwait(false);
        if (completion.Error is not null)
            return Result<ReceiverOutcome>.Failure(completion.Error);
        var (campaign, intent) = (completion.Campaign!, completion.Intent!);
        var now = clock.GetUtcNow();
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(CompleteAccessReviewCampaignClosure),
            command.Source.EventId, intent.Header, campaign.Stream, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        if (Decide(campaign, binding, command.OperationId) is { } early)
            return early;
        if (campaign.Pending is not { } pending || pending.Operation.RootOperationId != command.OperationId ||
            command.ItemProofs != intent.SourceUses.Content)
            return SourceUseReceiverSupport.Permanent("The closure differs from the pending completion admission.");

        AccessReviewCampaignCompletionView view;
        ImmutableContentRef proofs;
        try
        {
            var built = await BuildAsync(context, command, completion, tenant, now, ct).ConfigureAwait(false);
            if (built.Outcome is { } outcome)
                return outcome;
            (view, proofs) = (built.View!, built.Proofs!);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Transient($"The frozen completion cannot be verified yet: {exception.Message}");
        }

        var completionContent = await AccessReviewContent.WriteAsync(content, tenant,
            AccessReviewContent.CompletionSchema, view, ct).ConfigureAwait(false);
        var result = await AccessReviewContent.WriteResultAsync(content, tenant,
            AccessReviewContent.CompletionViewSchema, completionContent, ct).ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new AccessReviewCampaign(tenant, command.CampaignId,
                    command.CommandId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.Close(provenance, completionContent, proofs, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                    fresh.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The campaign changed concurrently; rehydrate and decide again, including against a cancellation.
            }
        }
    }

    async Task<Built> BuildAsync(IRequestContext<CompleteAccessReviewCampaignClosure> context,
        CompleteAccessReviewCampaignClosure command, AccessReviewCompletion completion, Uuid tenant, DateTimeOffset now,
        CancellationToken ct)
    {
        var (campaign, intent) = (completion.Campaign!, completion.Intent!);
        var launched = campaign.Launched!;
        var realm = command.Target.Realm;
        var items = await manifests.ItemsAsync(realm, launched.ItemsManifest, ct).ConfigureAwait(false);
        var requirements = await manifests.RequirementsAsync(realm, intent.SourceUses, ct).ConfigureAwait(false);
        if (requirements.Count != items.Count)
            return await AbortAsync(completion, command, "The pinned item set differs from the launched items.", null,
                context, ct).ConfigureAwait(false);

        var states = new List<AccessReviewItemState>(items.Count);
        var proofs = new List<AccessReviewItemCompletionProof>(items.Count);
        var ordered = items.Values
            .Select(item => (Item: item, Stream: AccessReviewEffects.ItemStream(tenant, command.CampaignId, item.ItemId)))
            .OrderBy(static pair => pair.Stream.Resource, Kernel.StreamKeyOrder.Instance);
        foreach (var (item, stream) in ordered)
        {
            if (!requirements.TryGetValue(stream.Resource, out var requirement))
                return await AbortAsync(completion, command, "A launched item is not covered by the completion.",
                    item.ItemId, context, ct).ConfigureAwait(false);
            var owner = await reader.HydrateAsync(new AccessReviewItem(tenant, command.CampaignId, item.ItemId).ForSourceUse(candidate => AccessReviewCompletionVerifier.UseKey(command.OperationId, campaign, requirement)), ct)
                .ConfigureAwait(false);
            var key = AccessReviewCompletionVerifier.UseKey(command.OperationId, campaign, requirement);
            if (owner.Opened is null || owner.Uses.Find(key) is not { IsActive: true } ||
                owner.Uses.GrantOf(key) is not { } grant)
                return new Built(null, null, SourceUseReceiverSupport.Transient(
                    $"The completion use of item {item.ItemId} is not held yet."));
            if (owner.Uses.Find(key)?.GrantFact is not AccessReviewItemCampaignCompletionUseAuthorized held ||
                held.Revision != owner.Revision ||
                !owner.MatchesPin(requirement.ContentEvent.EventId, requirement.ResourceRevision))
                return await AbortAsync(completion, command, $"Item {item.ItemId} changed under its completion hold.",
                    item.ItemId, context, ct).ConfigureAwait(false);
            var state = await AccessReviewItemFold.ReadAsync(owner, item, launched.RemediationOwnerMemberId, content,
                tenant, ct).ConfigureAwait(false);
            if (state.CompletionBlocker(now) is { } blocker)
                return await AbortAsync(completion, command, blocker, item.ItemId, context, ct).ConfigureAwait(false);
            // An item a drained bulk decision moved past its admission pin proves the facts it holds now.
            var facts = (ulong)owner.Revision == requirement.ResourceRevision
                ? await AccessReviewContent.ReadAsync<AccessReviewItemCompletionFacts>(content, tenant,
                    requirement.RequiredFacts, ct).ConfigureAwait(false)
                : new AccessReviewItemCompletionFacts(item.ItemId, state.Revision, state.CurrentDecision!.DecisionId,
                    state.CurrentDecision.Decision, state.RemediationStatus(now));
            states.Add(state);
            proofs.Add(new AccessReviewItemCompletionProof(item.ItemId, requirement, grant, facts));
        }

        var decision = await AccessReviewContent.ReadAsync<AccessReviewCompletionDecision>(content, tenant,
            intent.AuthoredDecision, ct).ConfigureAwait(false);
        var header = new AccessReviewCampaignResultHeader(campaign.Id, launched.Name, launched.Deadline,
            launched.SnapshotId, launched.ContentSha256, decision.Attestation);
        var asOf = intent.Header.AcceptedAt;
        var frozen = await freezer.FreezeAsync(context, tenant, AccessReviewCampaignContent.ResultKind,
            AccessReviewCampaignContent.ResultRows(header, [.. states.Select(state => state.ToView(asOf))]), null, null,
            intent.Header.OriginalActor, ct).ConfigureAwait(false);
        if (!frozen.IsSuccess)
            return new Built(null, null, Result<ReceiverOutcome>.Failure(frozen.Error!));
        var view = new AccessReviewCampaignCompletionView(frozen.Value.SnapshotId, frozen.Value.ContentSha256,
            decision.Attestation, intent.Header.OriginalActor, now);
        var proofSet = await AccessReviewContent.WriteAsync<IReadOnlyList<AccessReviewItemCompletionProof>>(content,
            tenant, AccessReviewContent.ItemProofsSchema, proofs, ct).ConfigureAwait(false);
        return new Built(view, proofSet, null);
    }

    async Task<Built> AbortAsync(AccessReviewCompletion completion, CompleteAccessReviewCampaignClosure command,
        string reason, Uuid? itemId, IExecutionContext context, CancellationToken ct) => new(null, null,
        await fences.AbortAsync(completion, command.Source, reason, itemId, context, ct).ConfigureAwait(false));

    static Result<ReceiverOutcome>? Decide(AccessReviewCampaign campaign, AcceptedOperationBinding binding,
        Uuid operationId)
    {
        switch (campaign.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(campaign.Stream,
                    campaign.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        if (campaign.CanceledFor(operationId) is { } canceled)
            return SourceUseReceiverSupport.Superseded("The completion was canceled; the generation is fenced.",
                NativeFactReference.Of(campaign.Stream, canceled));
        return campaign.ClosedFor(operationId) is { } closed
            ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(campaign.Stream, closed))
            : null;
    }

    sealed record Built(AccessReviewCampaignCompletionView? View, ImmutableContentRef? Proofs,
        Result<ReceiverOutcome>? Outcome);

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
