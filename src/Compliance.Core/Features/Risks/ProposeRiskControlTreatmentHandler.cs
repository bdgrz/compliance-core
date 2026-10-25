using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Asserts that the exact current approved version of a same-program control treats a risk whose chosen treatment is
///     mitigate. The command id is the treatment identity and the logical operation on the (risk, control) line, which
///     holds at most one pending proposal. The frozen first response is the registration after the proposal.
/// </summary>
public sealed class ProposeRiskControlTreatmentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    RiskGovernanceReader governanceReader, ControlActivationSource controls, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<ProposeRiskControlTreatment, RiskControlTreatmentRegistration>
{
    public async ValueTask<Result<RiskControlTreatmentRegistration>> HandleAsync(
        IRequestContext<ProposeRiskControlTreatment> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Fail(new RequestError(RequestErrorKind.Validation,
                "A treatment proposal requires a command id and a non-negative expected revision."));
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return Fail(risk.Error!);
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new RiskTreatmentAssertion(request.TenantId, request.ProgramId,
            request.RiskId, request.ControlId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "ProposeRiskControlTreatment", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? treatmentContent = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PreparePropose(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Fail(prepared.Error!);
            if (request.ControlId == Uuid.Empty || request.ControlVersionId == Uuid.Empty ||
                RiskCommands.InvalidText(request.Rationale))
                return Fail(CommandFailure.InvalidContent(
                    "A control treatment requires an approved control version and a rationale of at most 4000 characters.")
                    .ToRequestError());
            var control = await controls.LoadAsync(request.TenantId, request.ProgramId, request.ControlId, ct)
                .ConfigureAwait(false);
            if (!control.IsSuccess)
                return Fail(control.Error);
            if (control.Value.ApprovedVersion is not { Status: "approved" } approved ||
                approved.VersionId != request.ControlVersionId)
                return Fail(new RequestError(RequestErrorKind.Conflict,
                    "A control treatment must name the control's current approved version."));
            var evaluation = await reader.HydrateAsync(new RiskEvaluation(request.TenantId, request.RiskId), ct)
                .ConfigureAwait(false);
            if (evaluation.Header?.TreatmentKind != "mitigate")
                return Fail(CommandFailure.StateConflict(
                    "Choose the mitigate treatment before asserting a control treatment.").ToRequestError());
            var treatments = await governanceReader.ComposeTreatmentsAsync(request.TenantId, snapshot, ct)
                .ConfigureAwait(false);
            if (treatments.Any(item => item.Status == RiskStates.TreatmentAccepted &&
                    item.ControlVersionId == request.ControlVersionId))
                return Fail(CommandFailure.StateConflict(
                    "An accepted treatment assertion already names this exact control version.").ToRequestError());

            var view = new RiskControlTreatmentView(request.CommandId, request.RiskId, request.ControlId,
                request.ControlVersionId, RiskStates.TreatmentProposed, request.Rationale.Trim(), actorReference, now,
                null, null, null, null, null, null, null, null)
            { Revision = snapshot.Revision + 1 };
            treatmentContent = await FindingContent.WriteAsync(content, request.TenantId, RiskContent.TreatmentSchema,
                view, ct).ConfigureAwait(false);
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                RiskContent.ControlTreatmentRegistrationSchema,
                new RiskControlTreatmentRegistration(request.CommandId, snapshot.Revision + 1), ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAssertion>(binding,
            () => new RiskTreatmentAssertion(request.TenantId, request.ProgramId, request.RiskId, request.ControlId,
                request.CommandId),
            line => line.Acceptance,
            line => FindingCommands.Prepared(line.PreparePropose(request.ExpectedRevision), response),
            (RiskTreatmentAssertion line, ImmutableContentRef frozen) =>
                line.Propose(provenance, treatmentContent!, actor.MemberId, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return await FindingContent.ResponseAsync<RiskControlTreatmentRegistration>(content, request.TenantId, outcome,
            ct).ConfigureAwait(false);
    }

    static Result<RiskControlTreatmentRegistration> Fail(RequestError error) =>
        Result<RiskControlTreatmentRegistration>.Failure(error);
}
