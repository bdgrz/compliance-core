using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Admits native occurrence proofs before fresh sources, preserving original results and complete immutable bodies.</summary>
static class OccurrenceProofs
{
    public static async ValueTask<Result<ControlOccurrenceView>> AttestAsync(NativeOperationExecutor operations,
        IImmutableContentStore content, IExecutionContext context, string commandKind, Uuid commandId,
        long expectedRevision, string intentDigest, Uuid tenantId, Uuid programId, Uuid controlId, Uuid occurrenceId,
        OperationsActor actor, Func<CancellationToken, ValueTask<Result<PreparedOccurrenceProof>>> prepare,
        Func<PreparedOccurrenceProof, AttestationInput> inputOf, string? correctionReason,
        DateTimeOffset now, CancellationToken ct)
    {
        var correcting = correctionReason is not null;
        var stream = new ControlOccurrence(tenantId, programId, controlId, occurrenceId).Stream;
        var provenance = ControlOperationsSource.Provenance(commandId, commandKind, stream, actor,
            expectedRevision, intentDigest, now);
        ImmutableContentRef? attestation = null;
        ImmutableContentRef? result = null;
        ControlOccurrenceMaterialization? materialization = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOccurrence>(AcceptedOperationBinding.Of(provenance),
            () => new ControlOccurrence(tenantId, programId, controlId, occurrenceId, commandId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                try
                {
                    var prepared = await prepare(token).ConfigureAwait(false);
                    if (!prepared.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                    var source = prepared.Value!;
                    var current = source.Current;
                    if (current.TenantId != tenantId || current.ProgramId != programId || current.ControlId != controlId ||
                        current.OccurrenceId != occurrenceId || source.Actor.MemberId != actor.MemberId || current.Revision != owner.Revision)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The occurrence source changed while its exact native proof was prepared.", true));
                    var input = inputOf(source);
                    var failure = correcting
                        ? ControlOperationRules.CheckCorrection(current, expectedRevision, input, correctionReason!, source.Plan.ExpectedEvidence, now)
                        : ControlOperationRules.CheckAttestation(current, expectedRevision, input, source.Plan.ExpectedEvidence, now);
                    if (failure is not null)
                        return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                    var gate = owner.PrepareProof(expectedRevision, !correcting);
                    if (!gate.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(gate.Error!);
                    if (current.Revision == 0)
                        materialization = await MaterializationAsync(content, source, provenance, now, token).ConfigureAwait(false);
                    var view = ControlOperationRules.BuildAttestation(current.OccurrenceId, current.Attestations.Count + 1,
                        input, source.Plan, current.PeriodStart, current.PeriodEnd, correctionReason?.Trim(),
                        correcting ? current.Attestations[^1].AttestationId : null, actor.MemberId, actor.Display, now);
                    attestation = await ControlOperatingContent.WriteAsync(content, tenantId,
                        ControlOperatingContent.AttestationSchema, view, token).ConfigureAwait(false);
                    var frozen = await FreezeAsync(content, tenantId, ControlOccurrenceFold.WithAttestation(current, view,
                        current.Revision == 0 ? 2 : current.Revision + 1), token).ConfigureAwait(false);
                    result = frozen.Result;
                    return Result<FirstResponseDescriptor>.Success(frozen.Response);
                }
                catch (ImmutableContentIntegrityException failure)
                {
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact occurrence proof source cannot be verified.", failure is ImmutableContentUnavailableException));
                }
            }, (owner, frozen) => owner.Attest(provenance, attestation!, frozen, result!, materialization)), context, ct)
            .ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOccurrenceView>(content, tenantId, outcome, ct).ConfigureAwait(false);
    }

    public static async ValueTask<Result<ControlOccurrenceView>> ReviewAsync(NativeOperationExecutor operations,
        IImmutableContentStore content, IExecutionContext context, ReviewControlOccurrence request, string intentDigest,
        OperationsActor actor, Func<CancellationToken, ValueTask<Result<PreparedOccurrenceReview>>> prepare,
        DateTimeOffset now, CancellationToken ct)
    {
        var (tenantId, programId, controlId) = (request.TenantId, request.ProgramId, request.ControlId);
        var stream = new ControlOccurrence(tenantId, programId, controlId, request.OccurrenceId).Stream;
        var provenance = ControlOperationsSource.Provenance(request.CommandId, nameof(ReviewControlOccurrence), stream,
            actor, request.ExpectedRevision, intentDigest, now);
        ImmutableContentRef? review = null;
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOccurrence>(AcceptedOperationBinding.Of(provenance),
            () => new ControlOccurrence(tenantId, programId, controlId, request.OccurrenceId, request.CommandId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                try
                {
                    var prepared = await prepare(token).ConfigureAwait(false);
                    if (!prepared.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                    var current = prepared.Value!.Current;
                    if (current.TenantId != tenantId || current.ProgramId != programId || current.ControlId != controlId ||
                        current.OccurrenceId != request.OccurrenceId || current.Revision != owner.Revision)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The occurrence changed while its exact native review was prepared.", true));
                    var waiver = prepared.Value.Waiver;
                    var actions = request.RequestedActions ?? [];
                    var failure = ControlOperationRules.CheckReview(current, tenantId, request.ExpectedRevision,
                        request.AttestationId, request.Outcome, request.Rationale, actions, actor.MemberId, now, waiver);
                    if (failure is not null)
                        return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                    var gate = owner.PrepareProof(request.ExpectedRevision, false);
                    if (!gate.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(gate.Error!);
                    var attestation = current.Attestations[^1];
                    var view = new ControlOccurrenceReviewView(request.CommandId, request.AttestationId, attestation.Version,
                        request.Outcome, request.Rationale.Trim(), actions.Select(static item => item.Trim()).ToArray(),
                        actor.MemberId, ActorReference.ForMember(actor.MemberId, actor.Display), now, waiver?.Id);
                    review = await ControlOperatingContent.WriteAsync(content, tenantId,
                        ControlOperatingContent.ReviewSchema, view, token).ConfigureAwait(false);
                    var frozen = await FreezeAsync(content, tenantId,
                        ControlOccurrenceFold.WithReview(current, view, current.Revision + 1), token).ConfigureAwait(false);
                    result = frozen.Result;
                    return Result<FirstResponseDescriptor>.Success(frozen.Response);
                }
                catch (ImmutableContentIntegrityException failure)
                {
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact occurrence review source cannot be verified.", failure is ImmutableContentUnavailableException));
                }
            }, (owner, frozen) => owner.Review(provenance, review!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOccurrenceView>(content, tenantId, outcome, ct).ConfigureAwait(false);
    }

    static async Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> FreezeAsync(
        IImmutableContentStore content, Uuid tenantId, ControlOccurrenceView view, CancellationToken ct)
    {
        var body = await ControlOperatingContent.WriteAsync(content, tenantId,
            ControlOperatingContent.OccurrenceViewSchema, view, ct).ConfigureAwait(false);
        var result = await ControlOperatingContent.WriteResultAsync(content, tenantId,
            ControlOperatingContent.OccurrenceViewSchema, body, ct).ConfigureAwait(false);
        return (ControlOperatingContent.OkResponse(ControlOperatingContent.OccurrenceViewSchema, body), result);
    }

    static async Task<ControlOccurrenceMaterialization> MaterializationAsync(IImmutableContentStore content,
        PreparedOccurrenceProof prepared, OperationProvenance proof, DateTimeOffset now, CancellationToken ct)
    {
        var current = prepared.Current;
        var window = new ControlCalendarWindow(current.PeriodStart!.Value, current.PeriodEnd!.Value.AddDays(1));
        var operation = new OperationProvenance(Uuid.CreateVersion5(proof.LogicalId, "materialize"),
            proof.RootOperationId, null, "MaterializeControlOccurrence", proof.Actor, proof.Owner, proof.Episode,
            proof.OriginalExpectedRevision, proof.AuthoredIntentSha256, now, proof.Owner, proof.Owner, proof.Owner);
        var plan = prepared.Plan;
        var planStream = new ControlOperatingPlan(current.TenantId, current.ProgramId, current.ControlId).Stream;
        var intent = new ControlOccurrenceCreationIntent(
            new DurableIntentHeader(operation.RootOperationId, proof.Owner, proof.Owner, proof.Owner, proof.Actor,
                proof.Episode, proof.OriginalExpectedRevision, proof.AuthoredIntentSha256, now),
            current.TenantId, current.ProgramId, current.ControlId, current.OccurrenceId, window, plan.PlanVersionId,
            NativeFactReference.Of(planStream, prepared.Snapshot.Line(current.ControlId).Approvals[plan.PlanVersionId]),
            plan.Owner, plan.BackupOwner, plan.ReviewerMemberId, ControlOccurrenceCreationIntent.RecurringTrigger,
            now);
        var reference = await ControlOperatingContent.WriteAsync(content, current.TenantId,
            ControlOccurrenceCreationIntent.Schema, intent, ct).ConfigureAwait(false);
        return new ControlOccurrenceMaterialization(operation, new ControlOccurrenceTerms(current.Kind,
            current.ControlVersionId, current.PlanVersionId, current.PeriodStart, current.PeriodEnd, current.DueOn,
            null, current.Assignee, proof.Actor, now, window), reference);
    }

}
