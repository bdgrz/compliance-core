using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Adopts the exact computed assessment as the latest of the program. The program must still hold the exact pending
///     adoption generation the run reserved; the recorded assessment is read from its own owner and must carry the
///     frozen run intent. The registration result carries the assessment identity and the program revision after this
///     adoption, and the pre-admission revision is never compared with the advanced program. A newer generation proves
///     this adoption superseded; a changed intent or an assessment that is not the computation of this run is permanent.
/// </summary>
sealed class AdoptReadinessAssessmentHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, ReadinessVerifier verifier, TimeProvider clock)
    : IRequestHandler<AdoptReadinessAssessment, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AdoptReadinessAssessment> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.AssessmentId == Uuid.Empty ||
            command.ProgramId == Uuid.Empty || command.ExpectedRevision is not null)
            return SourceUseReceiverSupport.Permanent("The adoption names another operation, program or assessment.");
        var run = await verifier.VerifyRunAsync(command.RequestProof, command.Intent, command.OperationId,
            command.CommandId, ct).ConfigureAwait(false);
        if (run.Error is not null)
            return Result<ReceiverOutcome>.Failure(run.Error);
        var (intent, tenant) = (run.Intent!, run.TenantId);
        var assessment = await reader.HydrateAsync(new ReadinessAssessment(tenant, intent.ProgramId, intent.AssessmentId),
            ct).ConfigureAwait(false);
        if (command.ProgramId != intent.ProgramId || command.AssessmentId != intent.AssessmentId ||
            command.Generation != (long)intent.AdoptionGeneration || command.AdmissionGeneration != command.Generation ||
            run.Program!.Stream != command.Target || command.Source.Stream != assessment.Stream)
            return SourceUseReceiverSupport.Permanent("The adoption differs from the frozen run intent.");
        if (assessment.Recorded is not { } recorded)
            return SourceUseReceiverSupport.Transient("The computed assessment is not visible yet.");
        if (recorded.Metadata!.EventId != command.Source.EventId ||
            recorded.Metadata.AggregateVersion != command.Source.AggregateVersion || recorded.Intent != command.Intent ||
            recorded.AssessmentId != command.AssessmentId)
            return SourceUseReceiverSupport.Permanent("The source is not the computation of this run.");

        var provenance = ReadinessEffects.Child(command.CommandId, nameof(AdoptReadinessAssessment),
            command.Source.EventId, intent.Header, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var proofs = await ReadinessContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            ReadinessContent.AdoptionProofsSchema,
            (IReadOnlyList<NativeFactReference>)[command.RequestProof, command.Source], ct)
            .ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var program = await reader.HydrateAsync(new ProgramReadiness(tenant, intent.ProgramId, command.CommandId, command.OperationId),
                    ct).ConfigureAwait(false);
                if (Decide(program, binding, command) is { } early)
                    return early;
                var registration = await ReadinessContent.WriteDeterministicAsync(content, tenant,
                    Uuid.CreateVersion5(command.CommandId, $"registration:{program.Revision}"),
                    ReadinessContent.RegistrationSchema,
                    new ReadinessAssessmentRegistration(command.AssessmentId, program.Revision + 1), ct)
                    .ConfigureAwait(false);
                var result = await ReadinessContent.WriteResultAsync(content, tenant, ReadinessContent.RegistrationSchema,
                    registration, ct).ConfigureAwait(false);
                var step = await executor.ExecuteAsync(new ProgramReadiness(tenant, intent.ProgramId, command.CommandId, command.OperationId),
                    fresh =>
                    {
                        if (Decide(fresh, binding, command) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        if (fresh.Revision != program.Revision)
                            return AggregateOutcome.Discard(Result<Decided>.Failure(new RequestError(
                                RequestErrorKind.Conflict, "The program readiness changed while the adoption was prepared.",
                                true)));
                        var raised = fresh.Adopt(provenance, command.Intent, command.Generation, proofs, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                    fresh.AdoptionOf(command.AssessmentId)!)))))
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

    /// <summary>An exact replay, an adoption that already happened, or a proven newer generation.</summary>
    static Result<ReceiverOutcome>? Decide(ProgramReadiness program, AcceptedOperationBinding binding,
        AdoptReadinessAssessment command)
    {
        switch (program.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(program.Stream,
                    program.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        if (program.AdoptionOf(command.AssessmentId) is not null)
            return SourceUseReceiverSupport.Permanent("The assessment was adopted by another operation.");
        if (program.AdoptionGeneration > command.AdmissionGeneration)
            return SourceUseReceiverSupport.Superseded("A newer adoption generation superseded this run.", null);
        return program.PendingAdoption is { } pending && pending.Generation == command.AdmissionGeneration &&
               pending.Operation.RootOperationId == command.OperationId
            ? null
            : SourceUseReceiverSupport.Permanent("The program no longer holds this run's pending adoption generation.");
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
