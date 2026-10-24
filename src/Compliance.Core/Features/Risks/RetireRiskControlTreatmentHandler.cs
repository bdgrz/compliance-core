using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Withdraws an accepted control treatment assertion while keeping its history. Only the accepted treatment the
///     command names is retired, so a retirement never retires a newer successor or completes an action.
/// </summary>
public sealed class RetireRiskControlTreatmentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    RiskGovernanceReader governanceReader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RetireRiskControlTreatment>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RetireRiskControlTreatment> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A retirement requires a command id and a non-negative expected revision."));
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
        var provenance = FindingContent.Provenance(request.CommandId, "RetireRiskControlTreatment", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareRetire(request.TreatmentId, request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return prepared;
            if (RiskCommands.InvalidText(request.Rationale))
                return Result.Failure(CommandFailure.InvalidContent(
                    "Retiring a control treatment requires a rationale of at most 4000 characters.").ToRequestError());
            (response, result) = await RiskCommands.FreezeUnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAssertion>(binding,
            () => new RiskTreatmentAssertion(request.TenantId, request.ProgramId, request.RiskId, controlId,
                request.CommandId),
            line => line.Acceptance,
            line => FindingCommands.Prepared(line.PrepareRetire(request.TreatmentId, request.ExpectedRevision),
                response),
            (RiskTreatmentAssertion line, ImmutableContentRef frozen) => line.Retire(provenance, request.TreatmentId,
                request.Rationale.Trim(), actorReference, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
