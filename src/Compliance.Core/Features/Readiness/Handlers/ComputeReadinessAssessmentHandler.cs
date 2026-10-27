using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>
///     Computes the assessment of an admitted run on its own owner at the assessment's revision one. The frozen run
///     intent and its complete input snapshot are read from verified immutable content and evaluated with the rule
///     version they name; no live source is read, so a replay or a rerun after a crash produces the identical assessment.
///     An exact replay returns the original fact, a run that is not visible yet is transient, and a changed intent is a
///     permanent conflict.
/// </summary>
sealed class ComputeReadinessAssessmentHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, ReadinessVerifier verifier, TimeProvider clock)
    : IRequestHandler<ComputeReadinessAssessment, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ComputeReadinessAssessment> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.AssessmentId == Uuid.Empty ||
            command.ProgramId == Uuid.Empty || command.ExpectedRevision is not (null or 0))
            return SourceUseReceiverSupport.Permanent("The computation names another operation, program or assessment.");
        var run = await verifier.VerifyRunAsync(command.Source, command.Intent, command.OperationId, command.CommandId, ct)
            .ConfigureAwait(false);
        if (run.Error is not null)
            return Result<ReceiverOutcome>.Failure(run.Error);
        var (intent, tenant) = (run.Intent!, run.TenantId);
        var assessment = new ReadinessAssessment(tenant, intent.ProgramId, intent.AssessmentId, command.CommandId);
        if (command.ProgramId != intent.ProgramId || command.AssessmentId != intent.AssessmentId ||
            command.Generation != (long)intent.AdoptionGeneration || command.Target != assessment.Stream)
            return SourceUseReceiverSupport.Permanent("The computation differs from the frozen run intent.");
        if (intent.RuleVersion != ReadinessRules.Version)
            return SourceUseReceiverSupport.Permanent(
                "The frozen run names rules this version cannot evaluate; it never re-evaluates under other rules.");

        ReadinessEvaluation evaluation;
        try
        {
            evaluation = await EvaluateAsync(tenant, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen run inputs cannot be verified yet: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        var inputs = await ReadinessContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            ReadinessContent.InputsSchema, evaluation.Inputs, ct).ConfigureAwait(false);
        var findings = await ReadinessContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            ReadinessContent.FindingsSchema, evaluation.Findings, ct).ConfigureAwait(false);
        var gaps = await ReadinessContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            ReadinessContent.GapsSchema, evaluation.Gaps, ct).ConfigureAwait(false);
        var scopes = await ReadinessContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            ReadinessContent.AccessReviewScopesSchema, evaluation.AccessReviewScopes, ct).ConfigureAwait(false);
        var snapshot = await ReadinessContent.ReadAsync<ReadinessInputSnapshot>(content, tenant, intent.Inputs, ct)
            .ConfigureAwait(false);
        var provenance = ReadinessEffects.Child(command.CommandId, nameof(ComputeReadinessAssessment),
            command.Source.EventId, intent.Header, assessment.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new ReadinessAssessment(tenant, intent.ProgramId,
                    intent.AssessmentId, command.CommandId), ct).ConfigureAwait(false);
                if (Decide(current, binding) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new ReadinessAssessment(tenant, intent.ProgramId,
                    intent.AssessmentId, command.CommandId), fresh =>
                    {
                        if (Decide(fresh, binding) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.Record(provenance, intent.RuleVersion, snapshot.AsOf, snapshot.EditionId,
                            evaluation.InputFingerprint, inputs, findings, gaps, scopes, intent.RunnerMemberId,
                            intent.Header.OriginalActor, intent.Header.AcceptedAt, command.Intent);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream, fresh.Recorded!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The assessment changed concurrently; rehydrate and decide again.
            }
        }
    }

    async ValueTask<ReadinessEvaluation> EvaluateAsync(Uuid tenant, ReadinessRunIntent intent, CancellationToken ct)
    {
        var snapshot = await ReadinessContent.ReadAsync<ReadinessInputSnapshot>(content, tenant, intent.Inputs, ct)
            .ConfigureAwait(false);
        if (snapshot.ProgramId != intent.ProgramId || snapshot.AsOf != intent.AsOf)
            throw new ImmutableContentIntegrityException("The input snapshot is not the one this run froze.");
        var criteria = await ReadinessContent.ReadAsync<IReadOnlyList<Criteria.Models.Criterion>>(content, tenant,
            snapshot.Criteria, ct).ConfigureAwait(false);
        var mappings = await ReadinessContent.ReadAsync<IReadOnlyList<ControlMappings.Models.ControlCriterionMappingView>>(
            content, tenant, snapshot.Mappings, ct).ConfigureAwait(false);
        var effective = await ReadinessContent.ReadAsync<IReadOnlyList<ReadinessEffectiveControl>>(content, tenant,
            snapshot.EffectiveControls, ct).ConfigureAwait(false);
        var sources = await ReadinessContent.ReadAsync<ReadinessSourceSet>(content, tenant, snapshot.Sources, ct)
            .ConfigureAwait(false);
        var catalogEdition = snapshot.CatalogEdition is { } reference
            ? await ReadinessContent.ReadAsync<Criteria.Models.CriteriaCatalogEdition>(content, tenant, reference, ct)
                .ConfigureAwait(false)
            : null;
        var controls = effective.ToDictionary(static item => item.ControlId, static item => item.Version);
        return ReadinessRules.Evaluate(intent.ProgramId, snapshot.AsOf, snapshot.EditionId, criteria, mappings,
            controls, sources, catalogEdition);
    }

    /// <summary>An exact replay of this receiver command, or the permanent conflict of an assessment computed otherwise.</summary>
    static Result<ReceiverOutcome>? Decide(ReadinessAssessment assessment, AcceptedOperationBinding binding)
    {
        switch (assessment.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(assessment.Stream,
                    assessment.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return assessment.IsRecorded
            ? SourceUseReceiverSupport.Permanent("The readiness assessment was already computed by another operation.")
            : null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
