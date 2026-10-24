using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Completes the pending closure. Every participant's exact use must be granted and still held, which proves that
///     every admitted result was applied and that no result can append any more. The totals are then computed from the
///     complete applied results as of the closure date, and the closed campaign is appended with the complete
///     participant proofs. A cancellation recorded before this append fences it; an exact replay returns the original
///     fact. A use that is not visible yet is transient.
/// </summary>
sealed class CompletePolicyCampaignClosureHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, PolicyCampaignClosureVerifier verifier,
    CampaignSource campaigns) : IRequestHandler<CompletePolicyCampaignClosure, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CompletePolicyCampaignClosure> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.Generation < 1 ||
            command.ExpectedRevision is not null)
            return SourceUseReceiverSupport.Permanent("The completion names another generation or operation.");
        var closure = await verifier.VerifyAcceptedAsync(command.Source, command.Intent, command.OperationId,
            command.CommandId, ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return Result<ReceiverOutcome>.Failure(closure.Error);
        var (campaign, accepted, intent) = (closure.Campaign!, closure.Accepted!, closure.Intent!);
        if (command.Target != campaign.Stream || command.CampaignId != campaign.Id ||
            command.Generation != accepted.Generation || command.ParticipantProofManifest != intent.SourceUses.Content)
            return SourceUseReceiverSupport.Permanent("The completion differs from the frozen closure intent.");

        var provenance = PolicyCampaignContent.Child(command.CommandId, nameof(CompletePolicyCampaignClosure),
            command.Source.EventId, intent.Header, campaign.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new PolicyDistributionCampaign(campaign.TenantId, campaign.Id,
                    command.CommandId), ct).ConfigureAwait(false);
                if (Decide(current, binding, command.OperationId) is { } early)
                    return early;
                var built = await BuildAsync(current, closure, command, provenance, ct).ConfigureAwait(false);
                if (built.Error is not null)
                    return Result<ReceiverOutcome>.Failure(built.Error);
                if (built.Superseded is not null)
                    return built.Superseded.Value;
                var revision = current.Revision;
                var step = await executor.ExecuteAsync(new PolicyDistributionCampaign(campaign.TenantId, campaign.Id,
                    command.CommandId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(decided)));
                        if (fresh.Revision != revision)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(null)));
                        var raised = fresh.CompleteClosure(provenance, built.ClosedOn, built.Rationale!,
                            built.Totals!, intent.Header.OriginalActor, intent.Header.AcceptedAt, built.Proofs!,
                            command.Intent, built.Result!);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(SourceUseReceiverSupport.Applied(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Outcome is { } outcome)
                    return outcome;
                if (attempt >= MaximumAttempts)
                    return SourceUseReceiverSupport.Transient("The campaign kept changing while the closure completed.");
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The campaign changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>An exact replay, a conflicting binding, or a terminal fact that supersedes this completion.</summary>
    static Result<ReceiverOutcome>? Decide(PolicyDistributionCampaign campaign, AcceptedOperationBinding binding,
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
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected("The closure was canceled.",
                NativeFactReference.Of(campaign.Stream, canceled)));
        return campaign.Closing is { } closed && closed.Operation.RootOperationId == operationId
            ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(campaign.Stream, closed))
            : null;
    }

    async ValueTask<Built> BuildAsync(PolicyDistributionCampaign campaign, PolicyCampaignClosure closure,
        CompletePolicyCampaignClosure command, OperationProvenance provenance, CancellationToken ct)
    {
        var intent = closure.Intent!;
        var tenantId = campaign.TenantId;
        var proofs = new List<CampaignParticipantProof>();
        foreach (var participant in closure.Participants!)
        {
            var key = PolicyCampaignClosureEffects.UseKey(command.OperationId, intent.Header.FirstOwner,
                command.Generation, participant.Requirement.SourceOwner);
            var owner = await reader.HydrateAsync(new PolicyCampaignParticipant(tenantId, campaign.Id,
                participant.PersonId), ct).ConfigureAwait(false);
            if (owner.Uses.Find(key) is not { } entry)
                return Built.Failed(SourceUseReceiverSupport.Transient("A participant use is not visible yet.").Error!);
            if (entry.IsFence)
                return Built.Cancelled(SourceUseReceiverSupport.Superseded(
                    "A participant use was fenced before it was granted; the closure cannot complete.",
                    NativeFactReference.Of(owner.Stream, entry.SettleFact!)));
            if (!entry.IsActive || owner.Revision != (long)participant.Requirement.ResourceRevision)
                return Built.Failed(SourceUseReceiverSupport.Permanent(
                    "A participant use ended or changed before the closure completed.").Error!);
            proofs.Add(new CampaignParticipantProof(participant.PersonId, owner.Stream, owner.Revision,
                owner.Uses.GrantOf(key)!));
        }

        CloseCampaign authored;
        try
        {
            authored = await PolicyCampaignContent.ReadAsync<CloseCampaign>(content, tenantId,
                intent.AuthoredDecision, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Built.Failed(SourceUseReceiverSupport.Permanent(
                $"The frozen closure decision cannot be verified: {exception.Message}").Error!);
        }

        var state = await campaigns.StateOfAsync(campaign, ct).ConfigureAwait(false);
        if (!state.IsSuccess)
            return Built.Failed(state.Error!);
        var closedOn = DateOnly.FromDateTime(intent.Header.AcceptedAt.UtcDateTime);
        var totals = state.Value!.Totals(closedOn);
        var rationale = authored.Rationale.Trim();
        var totalsContent = await PolicyCampaignContent.WriteDerivedAsync(content, tenantId, command.CommandId,
            "closing-totals", PolicyCampaignContent.TotalsSchema, totals, ct).ConfigureAwait(false);
        var proofsContent = await PolicyCampaignContent.WriteDerivedAsync(content, tenantId, command.CommandId,
            "participant-proofs", PolicyCampaignContent.ParticipantProofsSchema,
            (IReadOnlyList<CampaignParticipantProof>)proofs, ct).ConfigureAwait(false);
        var view = state.Value.ToView(closedOn) with
        {
            Status = "closed",
            ClosedBy = intent.Header.OriginalActor,
            ClosedAt = intent.Header.AcceptedAt,
            ClosingTotals = totals,
            Revision = campaign.Revision + 1,
        };
        var body = await PolicyCampaignContent.WriteDerivedAsync(content, tenantId, command.CommandId,
            "campaign-view", PolicyCampaignContent.CampaignSchema, view, ct).ConfigureAwait(false);
        var result = await PolicyCampaignContent.WriteDerivedResultAsync(content, tenantId, command.CommandId,
            PolicyCampaignContent.CampaignSchema, body, ct).ConfigureAwait(false);
        return new Built(null, null, closedOn, rationale, totalsContent, proofsContent, result);
    }

    sealed record Step(Result<ReceiverOutcome>? Outcome);

    sealed record Built(RequestError? Error, Result<ReceiverOutcome>? Superseded, DateOnly ClosedOn,
        string? Rationale, ImmutableContentRef? Totals, ImmutableContentRef? Proofs, ImmutableContentRef? Result)
    {
        public static Built Failed(RequestError error) => new(error, null, default, null, null, null, null);

        public static Built Cancelled(Result<ReceiverOutcome> superseded) =>
            new(null, superseded, default, null, null, null, null);
    }
}
