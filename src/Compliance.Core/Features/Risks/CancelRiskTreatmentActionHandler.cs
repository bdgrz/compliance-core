using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Records an attributable cancellation of unfinished risk treatment work. Cancellation and completion submission
///     serialize on the action: of two racing writes at the same revision exactly one is accepted, and work whose
///     completion is under review cannot be canceled until the review settles.
/// </summary>
public sealed class CancelRiskTreatmentActionHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CancelRiskTreatmentAction>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CancelRiskTreatmentAction> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation requires a command id, an action id and a non-negative expected revision."));
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new RiskTreatmentAction(request.TenantId, request.ProgramId,
            request.RiskId, request.ActionId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "CancelRiskTreatmentAction", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareCancel(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return prepared;
            if (RiskCommands.InvalidText(request.Rationale))
                return Result.Failure(CommandFailure.InvalidContent(
                    "Cancelling a treatment action requires a cancellation ID and a rationale of at most 4000 characters.")
                    .ToRequestError());
            (response, result) = await RiskCommands.FreezeUnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAction>(binding,
            () => new RiskTreatmentAction(request.TenantId, request.ProgramId, request.RiskId, request.ActionId,
                request.CommandId),
            action => action.Acceptance,
            action => FindingCommands.Prepared(action.PrepareCancel(request.ExpectedRevision), response),
            (RiskTreatmentAction action, ImmutableContentRef frozen) => action.Cancel(provenance,
                request.Rationale.Trim(), actorReference, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
