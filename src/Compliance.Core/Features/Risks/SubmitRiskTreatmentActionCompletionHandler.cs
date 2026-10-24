using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Records that treatment work reached its target state, citing fulfilled evidence requests. The action stays
///     incomplete until an independent review accepts the exact submission, and a rejected completion can only be
///     resubmitted as a new operation. The command id is the submission identity.
/// </summary>
public sealed class SubmitRiskTreatmentActionCompletionHandler(NativeOperationExecutor operations,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<SubmitRiskTreatmentActionCompletion, RiskTreatmentActionCompletionRegistration>
{
    public async ValueTask<Result<RiskTreatmentActionCompletionRegistration>> HandleAsync(
        IRequestContext<SubmitRiskTreatmentActionCompletion> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Fail(new RequestError(RequestErrorKind.Forbidden, "Risk sign-off requires a personal HTTP invocation."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Fail(new RequestError(RequestErrorKind.Validation,
                "A completion requires a command id, an action id and a non-negative expected revision."));
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return Fail(risk.Error!);
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new RiskTreatmentAction(request.TenantId, request.ProgramId,
            request.RiskId, request.ActionId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "SubmitRiskTreatmentActionCompletion",
            snapshot.Stream, actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? evidenceManifest = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareSubmit(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Fail(prepared.Error!);
            var evidence = RiskCommands.Distinct(request.EvidenceRequestIds);
            if (request.CommandId == Uuid.Empty || RiskCommands.InvalidText(request.Summary) || evidence.Count == 0)
                return Fail(CommandFailure.InvalidContent(
                    "A completion requires a summary of at most 4000 characters and at least one evidence request.")
                    .ToRequestError());
            foreach (var id in evidence)
            {
                var cited = await reader.HydrateAsync(new EvidenceRequest(request.TenantId, request.ProgramId, id), ct)
                    .ConfigureAwait(false);
                if (cited.Status != EvidenceRequest.Fulfilled)
                    return Fail(CommandFailure.StateConflict(
                        "Every cited evidence request must be fulfilled before completion is submitted.")
                        .ToRequestError());
            }

            evidenceManifest = await FindingContent.WriteAsync<IReadOnlyList<Uuid>>(content, request.TenantId,
                RiskContent.SubmittedEvidenceSchema, evidence, ct).ConfigureAwait(false);
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                RiskContent.CompletionRegistrationSchema,
                new RiskTreatmentActionCompletionRegistration(request.CommandId, snapshot.Revision + 1), ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAction>(binding,
            () => new RiskTreatmentAction(request.TenantId, request.ProgramId, request.RiskId, request.ActionId,
                request.CommandId),
            action => action.Acceptance,
            action => FindingCommands.Prepared(action.PrepareSubmit(request.ExpectedRevision), response),
            (RiskTreatmentAction action, ImmutableContentRef frozen) => action.Submit(provenance,
                request.Summary.Trim(), evidenceManifest!, actor.MemberId, actorReference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<RiskTreatmentActionCompletionRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<RiskTreatmentActionCompletionRegistration> Fail(RequestError error) =>
        Result<RiskTreatmentActionCompletionRegistration>.Failure(error);
}
