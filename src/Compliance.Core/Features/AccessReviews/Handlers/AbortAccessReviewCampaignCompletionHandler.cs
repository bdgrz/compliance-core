using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Cancels the exact pending completion of a campaign and fences every delayed finalization of its generation
///     atomically, after an authoritative permanent proof mismatch. A completion that already closed is an
///     irreversible decision: the abort is a terminal no-op and the process recovers forward. The cancellation is the
///     fence proof the item uses are settled with; no source use is settled before it.
/// </summary>
sealed class AbortAccessReviewCampaignCompletionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AbortAccessReviewCampaignCompletion, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<AbortAccessReviewCampaignCompletion> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != AccessReviewEffects.Generation ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            !Uuid.TryParse(command.Target.Resource, CultureInfo.InvariantCulture, out var campaignId) ||
            command.Target != AccessReviewEffects.CampaignStream(tenant, campaignId))
            return SourceUseReceiverSupport.Permanent("The abort names another campaign stream, operation or generation.");

        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenant, campaignId, command.CommandId, command.OperationId), ct)
            .ConfigureAwait(false);
        if (campaign.AcceptedFor(command.OperationId) is not { } accepted)
            return SourceUseReceiverSupport.Transient("The accepted completion is not visible yet.");
        if (command.ExpectedRevision != accepted.Revision)
            return SourceUseReceiverSupport.Permanent("The abort differs from the frozen completion admission.");
        var header = AccessReviewIntentBindings.HeaderOf(accepted.Operation);
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(AbortAccessReviewCampaignCompletion),
            accepted.Metadata.EventId, header, campaign.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<AccessReviewCampaign, AccessReviewPreparedCut>(reader,
            () => new AccessReviewCampaign(tenant, campaignId, command.CommandId, command.OperationId),
            owner => Decide(owner, binding, command, false), async (owner, token) =>
            {
                var verified = await PrepareAsync(tenant, campaign, accepted, command, token).ConfigureAwait(false);
                return verified.IsSuccess ? Result<AccessReviewPreparedCut>.Success(AccessReviewPreparedCut.Capture(owner))
                    : Result<AccessReviewPreparedCut>.Failure(verified.Error!);
            }, ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        var preparedCut = preparation.Value.Value!;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new AccessReviewCampaign(tenant, campaignId, command.CommandId, command.OperationId), ct)
                    .ConfigureAwait(false);
                if (Decide(current, binding, command, true) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new AccessReviewCampaign(tenant, campaignId, command.CommandId, command.OperationId),
                    fresh =>
                    {
                        if (Decide(fresh, binding, command, true) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        if (!preparedCut.Matches(fresh))
                            return AggregateOutcome.Discard(Result<Decided>.Failure(AccessReviewPreparedCut.Changed().Error!));
                        var raised = fresh.CancelCompletion(provenance, command.FailureProof);
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
                // The campaign changed concurrently; rehydrate and decide again.
            }
        }
    }

    async ValueTask<Result<Verified>> PrepareAsync(Uuid tenant, AccessReviewCampaign campaign,
        AccessReviewCampaignCompletionAccepted accepted, AbortAccessReviewCampaignCompletion command, CancellationToken ct)
    {
        try
        {
            var intent = await AccessReviewContent.ReadAsync<ClosureIntent>(content, tenant, accepted.Intent, ct)
                .ConfigureAwait(false);
            var failure = await AccessReviewContent.ReadAsync<AccessReviewCompletionFailure>(content, tenant,
                command.FailureProof, ct).ConfigureAwait(false);
            AccessReviewIntentBindings.Require(intent.Header, accepted.Operation);
            if (intent.Header.RootOperationId != command.OperationId ||
                failure.Evidence != NativeFactReference.Of(campaign.Stream, accepted))
                return Result<Verified>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The abort differs from the frozen completion admission.", false));
            if (failure.ItemId is { } failedItem)
            {
                var requirements = await new AccessReviewManifests(content).RequirementsAsync(command.Target.Realm,
                    intent.SourceUses, ct).ConfigureAwait(false);
                if (!requirements.ContainsKey(AccessReviewEffects.ItemStream(tenant, campaign.Id, failedItem).Resource))
                    return Result<Verified>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The completion failure names an item outside its exact frozen selection.", false));
            }
            return Result<Verified>.Success(new Verified());
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<Verified>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen completion cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    static Result<ReceiverOutcome>? Decide(AccessReviewCampaign campaign, AcceptedOperationBinding binding,
        AbortAccessReviewCampaignCompletion command, bool prepared)
    {
        var canceled = campaign.CanceledFor(command.OperationId);
        if (!prepared && canceled?.SourceProofs != command.FailureProof)
            return null;
        switch (campaign.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(campaign.Stream,
                    campaign.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }
        if (canceled is not null)
            return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(campaign.Stream, canceled));
        return prepared && campaign.ClosedFor(command.OperationId) is { } closed
            ? SourceUseReceiverSupport.Superseded("The completion already closed the campaign; it recovers forward.",
                NativeFactReference.Of(campaign.Stream, closed)) : null;
    }

    sealed record Verified;

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
