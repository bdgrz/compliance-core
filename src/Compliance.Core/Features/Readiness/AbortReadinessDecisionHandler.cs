using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Cancels the exact pending decision after an authoritative permanent proof mismatch and fences its delayed
///     completion in the same append. A decision that was already recorded is an irreversible decision: the abort is a
///     terminal no-op and the decision recovers forward. A decision that is not visible yet is a transient wait.
/// </summary>
sealed class AbortReadinessDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AbortReadinessDecision, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AbortReadinessDecision> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.Generation < 1 ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The abort names another operation, generation or realm.");
        if (command.FailureProof.Schema != ReadinessContent.FailureSchema)
            return SourceUseReceiverSupport.Permanent("Cancellation requires the current exact admitted-decision failure schema.");
        ReadinessDecisionFailureProof proof;
        try
        {
            proof = await ReadinessContent.ReadAsync<ReadinessDecisionFailureProof>(content, tenant, command.FailureProof,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The failure proof cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (proof.ProgramId == Uuid.Empty || proof.AssessmentId == Uuid.Empty || proof.Admission is null ||
            proof.Intent is null || proof.GapId == Uuid.Empty || string.IsNullOrWhiteSpace(proof.Reason) ||
            new ProgramReadiness(tenant, proof.ProgramId).Stream != command.Target ||
            proof.Admission.Stream != command.Target || proof.OperationId != command.OperationId ||
            proof.Generation != command.Generation)
            return SourceUseReceiverSupport.Permanent("The failure proof names another exact program, root or generation.");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var program = await reader.HydrateAsync(new ProgramReadiness(tenant, proof.ProgramId, command.CommandId, command.OperationId),
                    ct).ConfigureAwait(false);
                if (program.AcceptedDecision(command.OperationId) is not { } accepted)
                    return SourceUseReceiverSupport.Transient("The accepted decision is not visible yet.");
                var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
                ReadinessDecisionIntent intent;
                try
                {
                    intent = await ReadinessContent.ReadAsync<ReadinessDecisionIntent>(content, tenant, terms.Intent, ct)
                        .ConfigureAwait(false);
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen decision cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
                }

                if (terms.Generation != command.Generation || intent.Header.RootOperationId != command.OperationId ||
                    intent.AssessmentId != proof.AssessmentId || terms.Intent != proof.Intent ||
                    NativeFactReference.Of(program.Stream, accepted) != proof.Admission ||
                    !ReadinessVerifier.HeaderMatches(intent.Header, ((INativeOperationFact)accepted).Operation))
                    return SourceUseReceiverSupport.Permanent("The abort differs from the accepted decision.");
                if (proof.GapId is { } gapId)
                {
                    try
                    {
                        var gaps = await ImmutableSelectionManifests.ReadAsync<ReadinessGapView>(content,
                            tenant.ToString(), intent.GapSelection, ct).ConfigureAwait(false);
                        if (gaps.All(gap => gap.GapId != gapId))
                            return SourceUseReceiverSupport.Permanent("The failure gap is outside the frozen decision selection.");
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact cancellation gap selection cannot be verified.", exception is ImmutableContentUnavailableException));
                    }
                }
                var provenance = ReadinessEffects.Child(command.CommandId, nameof(AbortReadinessDecision),
                    accepted.Metadata!.EventId, intent.Header, program.Stream, clock.GetUtcNow());
                var binding = AcceptedOperationBinding.Of(provenance);
                if (Decide(program, binding, command.OperationId, command.FailureProof) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new ProgramReadiness(tenant, proof.ProgramId, command.CommandId, command.OperationId),
                    fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId, command.FailureProof) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CancelDecision(provenance, command.FailureProof);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                    fresh.TerminalDecision(command.OperationId)!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The program readiness changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>An exact replay, a decision that was already recorded, or a decision canceled by another operation.</summary>
    static Result<ReceiverOutcome>? Decide(ProgramReadiness program, AcceptedOperationBinding binding, Uuid operationId,
        Proofs.ImmutableContentRef failureProof)
    {
        switch (program.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                if (program.Acceptance.SelectedFact is not ReadinessDecisionCanceled selected || selected.SourceProofs != failureProof)
                    return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(program.Stream,
                    program.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return program.TerminalDecision(operationId) switch
        {
            ReadinessDecisionRecorded or TypeIEntryDecisionRecorded => SourceUseReceiverSupport.Superseded(
                "The decision was already recorded; it recovers forward.",
                NativeFactReference.Of(program.Stream, program.TerminalDecision(operationId)!)),
            ReadinessDecisionCanceled canceled => SourceUseReceiverSupport.Replayed(NativeFactReference.Of(program.Stream,
                canceled)),
            _ => null,
        };
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
