using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Ends a pending decision that can definitely never be recorded: the decision is canceled first, which fences the
///     delayed completion against late delivery, and only then is every gap plan use settled as canceled, including the
///     uses that were never acquired, so a delayed acquisition is refused. All effects use the deterministic command ids
///     the process driver would use, so a later driver step replays them. A decision that was already recorded recovers
///     forward. Nothing is inferred from missing native state.
/// </summary>
sealed class ReadinessDecisionFences(IRequestBus bus, IImmutableContentStore content)
{
    public async ValueTask<Result<ReceiverOutcome>> AbortAsync(ReadinessDecision decision, NativeFactReference source,
        ImmutableContentRef intentReference, string reason, Uuid? gapId, IExecutionContext context, CancellationToken ct)
    {
        var (program, intent) = (decision.Program!, decision.Intent!);
        var episode = intent.Header.Episode;
        var proof = await ReadinessContent.WriteDeterministicAsync(content, decision.TenantId, intent.Header.RootOperationId,
            ReadinessContent.FailureSchema, new ReadinessDecisionFailureProof(intent.ProgramId, intent.AssessmentId,
                reason, gapId), ct).ConfigureAwait(false);
        var abortId = ReadinessEffects.DecisionReceiverCommandId(source.EventId, nameof(AbortReadinessDecision), null,
            episode);
        var aborted = await bus.SendAsync(new AbortReadinessDecision(abortId, intent.Header.RootOperationId,
            program.Stream, decision.Generation, 0, proof), context, ct).ConfigureAwait(false);
        if (!aborted.IsSuccess)
            return aborted;
        // A decision that was already recorded recovers forward: its own settlement releases the uses.
        if (aborted.Value is not (ReceiverOutcome.Applied or ReceiverOutcome.Replayed))
            return aborted;
        var canceled = aborted.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            _ => throw new InvalidOperationException("An abort that was not applied is handled above."),
        };
        var settled = await SettleCanceledAsync(decision, canceled, source, intentReference, context, ct)
            .ConfigureAwait(false);
        return settled.IsSuccess ? SourceUseReceiverSupport.Superseded(reason, canceled) : settled;
    }

    /// <summary>Settles every gap plan use of a canceled decision as canceled, in canonical key order.</summary>
    public async ValueTask<Result<ReceiverOutcome>> SettleCanceledAsync(ReadinessDecision decision,
        NativeFactReference canceled, NativeFactReference source, ImmutableContentRef intentReference,
        IExecutionContext context, CancellationToken ct)
    {
        var (program, intent) = (decision.Program!, decision.Intent!);
        var tenant = decision.TenantId;
        IReadOnlyList<SourceUseRequirement> uses;
        try
        {
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(content, tenant.ToString(),
                intent.RequiredPlanUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen plan uses cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        IReadOnlyList<ReadinessGapView> gaps;
        try
        {
            gaps = await ImmutableSelectionManifests.ReadAsync<ReadinessGapView>(content, tenant.ToString(),
                intent.GapSelection, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen gap set cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        foreach (var gap in gaps.OrderBy(item => ReadinessEffects.GapKey(tenant, intent.ProgramId, item.GapId),
                     Kernel.StreamKeyOrder.Instance))
        {
            var plan = new ReadinessGapPlan(tenant, intent.ProgramId, gap.GapId);
            if (uses.All(use => use.SourceOwner != plan.Stream))
                continue;
            var key = ReadinessEffects.GapKey(tenant, intent.ProgramId, gap.GapId);
            var settlementId = ReadinessEffects.DecisionReceiverCommandId(source.EventId,
                nameof(SettleReadinessGapPlanReadinessDecisionUse), key, intent.Header.Episode);
            var settled = await bus.SendAsync(new SettleReadinessGapPlanReadinessDecisionUse(settlementId, canceled,
                plan.Stream, intent.Header.RootOperationId, decision.Generation, null, intentReference, gap.GapId,
                intent.ProgramId, SourceUseSettlementDisposition.Canceled), context, ct).ConfigureAwait(false);
            if (!settled.IsSuccess)
                return settled;
        }

        return SourceUseReceiverSupport.Applied(canceled);
    }
}
