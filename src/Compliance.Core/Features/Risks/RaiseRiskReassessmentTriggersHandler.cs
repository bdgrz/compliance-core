using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Raises the durable reassessment trigger of exactly one assessed risk. The reactor owns the complete fan-out and
///     derives the command id from the source event and the risk, so this handler decides one risk and one trigger
///     identity only. The trigger identity is stable, so a second source for the same risk, kind and reference appends
///     nothing, and a risk that is not assessed on another method version has nothing to reassess.
/// </summary>
public sealed class RaiseRiskReassessmentTriggersHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RaiseRiskReassessmentTriggers>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RaiseRiskReassessmentTriggers> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.RiskId == Uuid.Empty ||
            request.TriggerKind is not (RiskStates.MethodChanged or RiskStates.BoundaryChanged) ||
            string.IsNullOrWhiteSpace(request.SourceReference))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A reassessment trigger requires a command id, a risk, a known kind and a source."));
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        var evaluation = await reader.HydrateAsync(new RiskEvaluation(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        if (evaluation.Latest(RiskEvaluation.Inherent) is not { } inherent ||
            request.MethodVersionId is { } method && inherent.MethodVersionId == method)
            return Result.Success;
        var triggerId = RiskGovernance.TriggerIdFor(request.ProgramId, request.RiskId, request.TriggerKind,
            request.SourceReference);
        var workload = request.TriggerKind == RiskStates.MethodChanged
            ? RiskMethodReassessmentReactor.WorkloadName
            : BoundaryRiskReassessmentReactor.WorkloadName;
        var actor = ActorReference.ForSystemProcess("reactor:" + workload, workload);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new RiskGovernance(request.TenantId, request.ProgramId,
            request.RiskId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "RaiseRiskReassessmentTriggers",
            snapshot.Stream, actor, null, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? triggerContent = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (snapshot.HasTrigger(triggerId))
                return Result.Success;
            try
            {
                triggerContent = await FindingContent.WriteDeterministicAsync(content, request.TenantId, triggerId,
                    RiskContent.TriggerSchema, new RiskReassessmentTriggerView(triggerId, request.RiskId,
                        request.TriggerKind, request.SourceReference, request.RaisedAt), ct).ConfigureAwait(false);
            }
            catch (ImmutableContentConflictException)
            {
                // A concurrent source wrote this trigger identity with other bytes; the retry finds the trigger.
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The reassessment trigger is being raised by another source; retry to confirm it.", true));
            }

            (response, result) = await RiskCommands.FreezeUnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<RiskGovernance>(binding,
            () => new RiskGovernance(request.TenantId, request.ProgramId, request.RiskId, request.CommandId),
            governance => governance.Acceptance,
            governance => FindingCommands.Prepared(governance.PrepareTrigger(triggerId), response),
            (RiskGovernance governance, ImmutableContentRef frozen) =>
                governance.RaiseTrigger(provenance, triggerId, triggerContent!, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
