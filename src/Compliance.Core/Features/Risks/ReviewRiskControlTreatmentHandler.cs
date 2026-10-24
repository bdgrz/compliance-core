using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Independently accepts or rejects a proposed control treatment assertion; HTTP-only. The proposer reviews only
///     under an exact <see cref="SeparationOfDutiesWaiverProof" /> that matches this treatment, control version,
///     assertion revision and reviewer, and a live waiver that does not prove that exact scope is refused.
/// </summary>
public sealed class ReviewRiskControlTreatmentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    RiskGovernanceReader governanceReader, ISeparationOfDutiesWaiverProofResolver waivers,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReviewRiskControlTreatment>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewRiskControlTreatment> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Risk sign-off requires a personal HTTP invocation."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A treatment review requires a command id and a non-negative expected revision."));
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        if (await governanceReader.LocateControlAsync(request.TenantId, request.ProgramId, request.RiskId,
                request.TreatmentId, ct).ConfigureAwait(false) is not { } controlId)
            return Result.Failure(CommandFailure.MissingRecord("The risk control treatment was not found.")
                .ToRequestError());
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new RiskTreatmentAssertion(request.TenantId, request.ProgramId,
            request.RiskId, controlId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "ReviewRiskControlTreatment", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareReview(request.TreatmentId, request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return prepared;
            var treatment = snapshot.Find(request.TreatmentId)!;
            var proposed = await FindingContent.ReadAsync<RiskControlTreatmentView>(content, request.TenantId,
                treatment.Proposed.TreatmentContent, ct).ConfigureAwait(false);
            if (request.SeparationOfDutiesWaiverId is { } waiverId)
            {
                var scope = new SeparationOfDutiesWaiverProofScope(request.TenantId, waiverId, actor.MemberId,
                    actor.MemberId, SeparationOfDutiesActions.Review, SeparationOfDutiesRecordTypes.RiskControlTreatment,
                    request.TreatmentId, proposed.ControlVersionId, request.ExpectedRevision);
                var proof = await waivers.ResolveAsync(scope, now, ct).ConfigureAwait(false);
                if (!proof.IsSuccess)
                    return Result.Failure(CommandFailure.ActorProhibited(
                        "The separation-of-duties waiver is not active for this member and treatment revision.")
                        .ToRequestError());
            }
            else if (treatment.ProposerMemberId == actor.MemberId)
                return Result.Failure(CommandFailure.ActorProhibited(
                    "A control treatment proposer cannot review their own assertion.").ToRequestError());

            if (request.Outcome is not (RiskStates.Accept or RiskStates.Reject) ||
                RiskCommands.InvalidText(request.Rationale))
                return Result.Failure(CommandFailure.InvalidContent(
                    "A treatment review requires an outcome of accept or reject and a rationale of at most 4000 characters.")
                    .ToRequestError());
            (response, result) = await RiskCommands.FreezeUnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAssertion>(binding,
            () => new RiskTreatmentAssertion(request.TenantId, request.ProgramId, request.RiskId, controlId,
                request.CommandId),
            line => line.Acceptance,
            line => FindingCommands.Prepared(line.PrepareReview(request.TreatmentId, request.ExpectedRevision),
                response),
            (RiskTreatmentAssertion line, ImmutableContentRef frozen) => line.Review(provenance, request.TreatmentId,
                request.Outcome, request.Rationale.Trim(), actorReference, now, request.SeparationOfDutiesWaiverId,
                frozen, result!)), context, ct).ConfigureAwait(false);
        return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
