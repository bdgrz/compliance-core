using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>
///     Admits the acting member's own readiness decision; HTTP-only, never an MCP tool. The decision seals the latest
///     adoption and the gap plans and is acknowledged with 202; <c>ReadinessDecisionProcess</c> records it once every
///     gap plan is held.
/// </summary>
sealed class DecideReadinessHandler(ReadinessDecisionAdmission admission)
    : IRequestHandler<DecideReadiness, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<DecideReadiness> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result<OperationAcceptedResponse>.Failure(new RequestError(
                RequestErrorKind.Forbidden, "Readiness sign-off requires a personal HTTP invocation.")));
        var request = context.Request;
        return admission.AdmitAsync(ReadinessDecisionKind.Readiness, context, request, request.CommandId,
            request.TenantId, request.ProgramId, request.AssessmentId, request.ExpectedRevision, request.Outcome,
            request.Rationale, [], request.SeparationOfDutiesWaiverId, ct);
    }
}
