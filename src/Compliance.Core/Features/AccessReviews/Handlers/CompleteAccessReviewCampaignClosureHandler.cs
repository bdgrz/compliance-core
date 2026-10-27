using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.History;
using Bdgrz.Compliance.Features.AccessReviews.Models;
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
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
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
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Closes the campaign after every item was held. Under the holds it verifies that every frozen item exists and
///     that its grant is the exact one the completion pinned, re-reads each item's decision, remediation and
///     exception proof (an exception is judged at close time), freezes the final campaign snapshot and appends the
///     completion with the complete proof set. A permanent proof mismatch cancels the completion and fences it; a
///     fact that is not visible yet is transient and a closure that already happened replays.
/// </summary>
sealed class CompleteAccessReviewCampaignClosureHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AccessReviewManifests manifests, PopulationSnapshotFreezer freezer,
    AccessReviewCompletionVerifier verifier, AccessReviewCompletionFences fences, TimeProvider clock, IDomainEventReader events)
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
        var completion = await verifier.ReadNativeAsync(tenant, command.CampaignId, command.Source, command.Intent,
            command.OperationId, command.CommandId, ct).ConfigureAwait(false);
        if (completion.Error is not null)
            return Result<ReceiverOutcome>.Failure(completion.Error);
        completion = await verifier.BindAsync(tenant, completion, ct).ConfigureAwait(false);
        if (completion.Error is not null)
            return Result<ReceiverOutcome>.Failure(completion.Error);
        var (campaign, intent) = (completion.Campaign!, completion.Intent!);
        if (command.ItemProofs != intent.SourceUses.Content)
            return SourceUseReceiverSupport.Permanent("The closure differs from the frozen completion item manifest.");
        var now = clock.GetUtcNow();
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(CompleteAccessReviewCampaignClosure),
            command.Source.EventId, intent.Header, campaign.Stream, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<AccessReviewCampaign, PreparedClosure>(reader,
            () => new AccessReviewCampaign(tenant, command.CampaignId, command.CommandId, command.OperationId),
            owner => Decide(owner, binding, command.OperationId),
            (owner, token) => PrepareAsync(context, command, completion with { Campaign = owner }, tenant, now, token), ct)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        var prepared = preparation.Value.Value!;
        if (prepared.Outcome is { } outcome)
            return outcome;
        var (preparedCut, completionContent, proofs, result) = (prepared.Cut!, prepared.Content!, prepared.Proofs!, prepared.Result!);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new AccessReviewCampaign(tenant, command.CampaignId,
                    command.CommandId, command.OperationId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        if (!preparedCut.Matches(fresh))
                            return AggregateOutcome.Discard(Result<Decided>.Failure(AccessReviewPreparedCut.Changed().Error!));
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

    async ValueTask<Result<PreparedClosure>> PrepareAsync(IRequestContext<CompleteAccessReviewCampaignClosure> context,
        CompleteAccessReviewCampaignClosure command, AccessReviewCompletion completion, Uuid tenant, DateTimeOffset now,
        CancellationToken ct)
    {
        var verified = await verifier.PrepareAsync(tenant, completion, ct).ConfigureAwait(false);
        if (verified.Error is not null)
            return Result<PreparedClosure>.Failure(verified.Error);
        if (verified.Campaign!.Pending is not { } pending || pending.Operation.RootOperationId != command.OperationId)
            return Result<PreparedClosure>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The closure differs from the pending completion admission.", false));
        var cut = AccessReviewPreparedCut.Capture(verified.Campaign);
        try
        {
            var built = await BuildAsync(context, command, verified, tenant, now, ct).ConfigureAwait(false);
            if (built.Outcome is { } outcome)
                return outcome.IsSuccess
                    ? Result<PreparedClosure>.Success(new PreparedClosure(null, null, null, null, outcome))
                    : Result<PreparedClosure>.Failure(outcome.Error!);
            var completionContent = await AccessReviewContent.WriteAsync(content, tenant,
                AccessReviewContent.CompletionSchema, built.View!, ct).ConfigureAwait(false);
            var result = await AccessReviewContent.WriteResultAsync(content, tenant,
                AccessReviewContent.CompletionViewSchema, completionContent, ct).ConfigureAwait(false);
            return Result<PreparedClosure>.Success(new PreparedClosure(cut, completionContent, built.Proofs!, result, null));
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<PreparedClosure>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen completion cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
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
            .OrderBy(static pair => pair.Stream.Resource, Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance);
        foreach (var (item, stream) in ordered)
        {
            if (!requirements.TryGetValue(stream.Resource, out var requirement))
                return await AbortAsync(completion, command, "A launched item is not covered by the completion.",
                    item.ItemId, context, ct).ConfigureAwait(false);
            var owner = await reader.HydrateAsync(new AccessReviewItem(tenant, command.CampaignId, item.ItemId, selectedContentEventId: requirement.ContentEvent.EventId).ForSourceUse(candidate => AccessReviewCompletionVerifier.UseKey(command.OperationId, campaign, requirement)), ct)
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
            var history = await AccessReviewItemHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
            var state = await AccessReviewItemFold.ReadAsync(owner, history, item, launched.RemediationOwnerMemberId, content,
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

    sealed record PreparedClosure(AccessReviewPreparedCut? Cut, ImmutableContentRef? Content, ImmutableContentRef? Proofs,
        ImmutableContentRef? Result, Result<ReceiverOutcome>? Outcome);

    sealed record Built(AccessReviewCampaignCompletionView? View, ImmutableContentRef? Proofs,
        Result<ReceiverOutcome>? Outcome);

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
