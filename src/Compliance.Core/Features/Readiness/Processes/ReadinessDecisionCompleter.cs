using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Readiness.Services;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Readers;
namespace Bdgrz.Compliance.Features.Readiness.Processes;

/// <summary>
///     Records an admitted decision once every frozen gap plan is held under this decision's grant. The held plan proofs
///     are re-derived from the authoritative gap plans and must equal the manifest the command carries; every rule and
///     the exact waiver proof, when one was required, is verified again at the declared decision instant over the frozen
///     gap set. A decision canceled by a fence is a terminal no-op, a plan that is no longer exactly the frozen plan or
///     a waiver that no longer verifies is a permanent mismatch that cancels the decision, and a grant that is not
///     visible yet is a transient wait. The decision is appended before any use is settled.
/// </summary>
sealed class ReadinessDecisionCompleter(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, ISeparationOfDutiesWaiverProofResolver waivers, ReadinessVerifier verifier,
    ReadinessDecisionFences fences, ReadinessReader readiness, TimeProvider clock)
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> CompleteAsync(ReadinessDecisionKind kind, string commandKind,
        Uuid commandId, NativeFactReference source, EventStreamAddress target, Uuid operationId, long generation,
        ImmutableContentRef intentReference, Uuid programId, Uuid assessmentId, ImmutableContentRef planProofs,
        IExecutionContext context, CancellationToken ct)
    {
        if (commandId == Uuid.Empty || operationId == Uuid.Empty || programId == Uuid.Empty || assessmentId == Uuid.Empty)
            return SourceUseReceiverSupport.Permanent("The completion names another operation.");
        var decision = await verifier.VerifyAcceptedAsync(source, intentReference, operationId, commandId, ct)
            .ConfigureAwait(false);
        if (decision.Error is not null)
            return Result<ReceiverOutcome>.Failure(decision.Error);
        var (program, intent, tenant) = (decision.Program!, decision.Intent!, decision.TenantId);
        if (target != program.Stream || programId != intent.ProgramId || assessmentId != intent.AssessmentId ||
            generation != decision.Generation || intent.DecisionKind != kind)
            return SourceUseReceiverSupport.Permanent("The completion differs from the frozen decision intent.");

        var provenance = ReadinessEffects.Child(commandId, commandKind, source.EventId, intent.Header, program.Stream,
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        if (Decide(program, binding, operationId) is { } early)
            return early;

        var held = await ReadinessDecisionProofs.HeldAsync(reader, content, decision, ct).ConfigureAwait(false);
        if (held.Proofs is null)
            return held.Transient || held.GapId is null
                ? Result<ReceiverOutcome>.Failure(held.Error)
                : await fences.AbortAsync(decision, source, intentReference, held.Reason!, held.GapId, context, ct)
                    .ConfigureAwait(false);
        var proofs = await ReadinessContent.WriteDeterministicAsync(content, tenant, commandId,
            ReadinessContent.DecisionProofsSchema, held.Proofs, ct).ConfigureAwait(false);
        if (proofs != planProofs)
            return SourceUseReceiverSupport.Permanent(
                "The held plan proofs differ from the manifest the decision was completed with.");

        IReadOnlyList<ReadinessGapView> gaps;
        IReadOnlyList<Uuid> acknowledged;
        ReadinessAssessmentRecorded recorded;
        string? decidedOutcome;
        var enteredType1 = false;
        SeparationOfDutiesWaiverProof? waiverProof = null;
        try
        {
            gaps = await ImmutableSelectionManifests.ReadAsync<ReadinessGapView>(content, tenant.ToString(),
                intent.GapSelection, ct).ConfigureAwait(false);
            acknowledged = await ImmutableSelectionManifests.ReadAsync<Uuid>(content, tenant.ToString(),
                intent.AcknowledgedGapIds, ct).ConfigureAwait(false);
            var assessment = await reader.HydrateAsync(new ReadinessAssessment(tenant, programId, assessmentId), ct)
                .ConfigureAwait(false);
            recorded = assessment.Recorded ?? throw new ImmutableContentIntegrityException(
                "The decided assessment has no recorded content.");
            decidedOutcome = (await readiness.ReadDecisionAsync(tenant, program, assessmentId, ct).ConfigureAwait(false))
                ?.Outcome;
            if (kind == ReadinessDecisionKind.TypeIEntry)
                enteredType1 = (await readiness.ReadEntryDecisionsAsync(tenant, program, ct).ConfigureAwait(false))
                    .Any(static entry => entry.Outcome != ProgramReadiness.Defer);
            if (intent.WaiverProof is { } waiverReference)
                waiverProof = await ReadinessContent.ReadAsync<SeparationOfDutiesWaiverProof>(content, tenant,
                    waiverReference, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen decision content cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (waiverProof is not null)
        {
            var verified = await waivers.VerifyAsync(waiverProof, intent.Header.AcceptedAt, ct).ConfigureAwait(false);
            if (!verified.IsSuccess)
                return verified.Error!.IsTransient
                    ? Result<ReceiverOutcome>.Failure(verified.Error)
                    : await fences.AbortAsync(decision, source, intentReference,
                        "The separation-of-duties waiver proof no longer verifies.", null, context, ct)
                        .ConfigureAwait(false);
        }

        var decider = ReadinessContent.MemberOf(intent.Header);
        var failure = ReadinessDecisionRules.Validate(kind, intent.Outcome, intent.Rationale, gaps, acknowledged,
            intent.RuleVersion, decidedOutcome, enteredType1, recorded.RunnerMemberId != decider || waiverProof is not null);
        if (failure is not null)
            return await fences.AbortAsync(decision, source, intentReference, failure.Message!, null, context, ct)
                .ConfigureAwait(false);

        var (contentReference, result) = await WriteDecisionAsync(kind, tenant, commandId, intent, recorded, gaps,
            acknowledged, decider, waiverProof, ct).ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new ProgramReadiness(tenant, programId, commandId, operationId), fresh =>
                    {
                        if (Decide(fresh, binding, operationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = kind == ReadinessDecisionKind.Readiness
                            ? fresh.RecordReadinessDecision(provenance, contentReference, assessmentId, proofs,
                                intentReference, result)
                            : fresh.RecordTypeIEntryDecision(provenance, contentReference, assessmentId, proofs,
                                intentReference, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                    fresh.TerminalDecision(operationId)!)))))
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

    async ValueTask<(ImmutableContentRef Content, ImmutableContentRef Result)> WriteDecisionAsync(
        ReadinessDecisionKind kind, Uuid tenant, Uuid commandId, ReadinessDecisionIntent intent,
        ReadinessAssessmentRecorded recorded, IReadOnlyList<ReadinessGapView> gaps, IReadOnlyList<Uuid> acknowledged,
        Uuid decider, SeparationOfDutiesWaiverProof? waiver, CancellationToken ct)
    {
        var rationale = intent.Rationale.Trim();
        ImmutableContentRef body;
        string schema;
        if (kind == ReadinessDecisionKind.Readiness)
        {
            schema = ReadinessContent.ReadinessDecisionSchema;
            body = await ReadinessContent.WriteDeterministicAsync(content, tenant, commandId, schema,
                new ReadinessDecisionView(intent.Header.RootOperationId, intent.AssessmentId, intent.Outcome, rationale,
                    decider, intent.Header.OriginalActor, intent.Header.AcceptedAt, waiver?.WaiverId), ct)
                .ConfigureAwait(false);
        }
        else
        {
            schema = ReadinessContent.TypeIEntryDecisionSchema;
            var acknowledgedSet = acknowledged.ToHashSet();
            var items = gaps.Select(gap => new TypeIEntryUnresolvedItemView(gap.GapId, gap.Kind, gap.Subject, gap.RuleId,
                gap.Explanation, gap.Plan?.OwnerMemberId, gap.Plan?.TargetDate, acknowledgedSet.Contains(gap.GapId)))
                .ToArray();
            body = await ReadinessContent.WriteDeterministicAsync(content, tenant, commandId, schema,
                new TypeIEntryDecisionView(intent.Header.RootOperationId, intent.AssessmentId, recorded.RuleVersion,
                    recorded.AsOf, recorded.InputFingerprint, intent.Outcome, rationale, items, decider,
                    intent.Header.OriginalActor, intent.Header.AcceptedAt, waiver?.WaiverId), ct).ConfigureAwait(false);
        }

        var viewSchema = kind == ReadinessDecisionKind.Readiness
            ? ReadinessContent.ReadinessDecisionViewSchema
            : ReadinessContent.TypeIEntryDecisionViewSchema;
        var result = await content.WriteJsonAsync(ReadinessContent.Realm(tenant),
            Uuid.CreateVersion5(commandId, "mutation-result"), "mutation-result-content.v1",
            new MutationResultContent(viewSchema, new StandardResponseBody(body)), ct).ConfigureAwait(false);
        return (body, result);
    }

    /// <summary>An exact replay, or a decision that was canceled and so can never be recorded.</summary>
    static Result<ReceiverOutcome>? Decide(ProgramReadiness program, AcceptedOperationBinding binding, Uuid operationId)
    {
        switch (program.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(program.Stream,
                    program.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return program.TerminalDecision(operationId) switch
        {
            ReadinessDecisionCanceled canceled => Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(
                "The decision was canceled; settle its plan uses.", NativeFactReference.Of(program.Stream, canceled))),
            ReadinessDecisionRecorded or TypeIEntryDecisionRecorded => SourceUseReceiverSupport.Permanent(
                "The decision was recorded by another operation."),
            _ => null,
        };
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
