using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Records an attestation, a correction or a review as the root native fact of its command on the occurrence's
///     own stream. The frozen first response is the complete occurrence view after the fact, so an exact retry returns
///     the original view even after later changes. An expected occurrence that is not materialized yet is
///     materialized first, in the same batch, from its approved plan.
/// </summary>
static class OccurrenceProofs
{
    public static async ValueTask<Result<ControlOccurrenceView>> AttestAsync(NativeOperationExecutor operations,
        IAggregateReader reader, IImmutableContentStore content, IExecutionContext context, string commandKind, Uuid commandId,
        long expectedRevision, string intentDigest, PreparedOccurrenceProof prepared, AttestationInput input,
        string? correctionReason, DateTimeOffset now, CancellationToken ct)
    {
        var current = prepared.Current;
        var (tenantId, programId, controlId) = (current.TenantId, current.ProgramId, current.ControlId);
        var correcting = correctionReason is not null;
        if (correcting && current.Revision == 0)
            return Failure(RequestErrorKind.NotFound, "The control occurrence was not found.");
        var stream = new ControlOccurrence(tenantId, programId, controlId, current.OccurrenceId).Stream;
        var provenance = ControlOperationsSource.Provenance(commandId, commandKind, stream, prepared.Actor,
            expectedRevision, intentDigest, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var occurrence = new ControlOccurrence(tenantId, programId, controlId, current.OccurrenceId, commandId);
        var hydrated = await reader.HydrateAsync(occurrence, ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? attestation = null;
        ImmutableContentRef? result = null;
        ControlOccurrenceMaterialization? materialization = null;
        if (hydrated.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var failure = correcting
                ? ControlOperationRules.CheckCorrection(current, expectedRevision, input, correctionReason!,
                    prepared.Plan.ExpectedEvidence, now)
                : ControlOperationRules.CheckAttestation(current, expectedRevision, input,
                    prepared.Plan.ExpectedEvidence, now);
            if (failure is not null)
                return Result<ControlOccurrenceView>.Failure(failure.ToRequestError());
            if (current.Revision == 0)
                materialization = await MaterializationAsync(content, prepared, provenance, now, ct)
                    .ConfigureAwait(false);
            var version = current.Attestations.Count + 1;
            var view = ControlOperationRules.BuildAttestation(current.OccurrenceId, version, input, prepared.Plan,
                current.PeriodStart, current.PeriodEnd, correctionReason?.Trim(),
                correcting ? current.Attestations[^1].AttestationId : null, prepared.Actor.MemberId,
                prepared.Actor.Display, now);
            attestation = await ControlOperatingContent.WriteAsync(content, tenantId,
                ControlOperatingContent.AttestationSchema, view, ct).ConfigureAwait(false);
            var after = ControlOccurrenceFold.WithAttestation(current, view,
                current.Revision == 0 ? 2 : current.Revision + 1);
            (response, result) = await FreezeAsync(content, tenantId, after, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOccurrence>(binding,
            () => new ControlOccurrence(tenantId, programId, controlId, current.OccurrenceId, commandId),
            owner => owner.Acceptance, owner => Prepare(owner, expectedRevision, current.Revision == 0, response),
            (ControlOccurrence owner, ImmutableContentRef frozen) => owner.Attest(provenance, attestation!, frozen,
                result!, materialization)), context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOccurrenceView>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    public static async ValueTask<Result<ControlOccurrenceView>> ReviewAsync(NativeOperationExecutor operations,
        IAggregateReader reader, IImmutableContentStore content, IExecutionContext context, ReviewControlOccurrence request,
        string intentDigest, ControlOccurrenceView current, OperationsActor actor, SeparationOfDutiesWaiver? waiver,
        DateTimeOffset now, CancellationToken ct)
    {
        var (tenantId, programId, controlId) = (request.TenantId, request.ProgramId, request.ControlId);
        var stream = new ControlOccurrence(tenantId, programId, controlId, current.OccurrenceId).Stream;
        var provenance = ControlOperationsSource.Provenance(request.CommandId, "ReviewControlOccurrence", stream,
            actor, request.ExpectedRevision, intentDigest, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var hydrated = await reader.HydrateAsync(new ControlOccurrence(tenantId, programId, controlId,
            current.OccurrenceId, request.CommandId), ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? review = null;
        ImmutableContentRef? result = null;
        if (hydrated.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (current.Revision == 0)
                return Failure(RequestErrorKind.NotFound, "The control occurrence was not found.");
            var actions = request.RequestedActions ?? [];
            var failure = ControlOperationRules.CheckReview(current, tenantId, request.ExpectedRevision,
                request.AttestationId, request.Outcome, request.Rationale, actions, actor.MemberId, now, waiver);
            if (failure is not null)
                return Result<ControlOccurrenceView>.Failure(failure.ToRequestError());
            var attestation = current.Attestations[^1];
            var view = new ControlOccurrenceReviewView(request.CommandId, request.AttestationId, attestation.Version,
                request.Outcome, request.Rationale.Trim(), actions.Select(static action => action.Trim()).ToArray(),
                actor.MemberId, ActorReference.ForMember(actor.MemberId, actor.Display), now, waiver?.Id);
            review = await ControlOperatingContent.WriteAsync(content, tenantId,
                ControlOperatingContent.ReviewSchema, view, ct).ConfigureAwait(false);
            (response, result) = await FreezeAsync(content, tenantId,
                ControlOccurrenceFold.WithReview(current, view, current.Revision + 1), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOccurrence>(binding,
            () => new ControlOccurrence(tenantId, programId, controlId, current.OccurrenceId, request.CommandId),
            owner => owner.Acceptance, owner => Prepare(owner, request.ExpectedRevision, false, response),
            (ControlOccurrence owner, ImmutableContentRef frozen) => owner.Review(provenance, review!, frozen,
                result!)), context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOccurrenceView>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
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
            proof.OriginalExpectedRevision, proof.AuthoredIntentSha256, now, proof.Owner, proof.Owner);
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

    static Result<FirstResponseDescriptor> Prepare(ControlOccurrence owner, long expectedRevision,
        bool mayMaterialize, FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareProof(expectedRevision, mayMaterialize);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The occurrence changed while the proof was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<ControlOccurrenceView> Failure(RequestErrorKind kind, string message) =>
        Result<ControlOccurrenceView>.Failure(new RequestError(kind, message));
}
