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
///     Admits the acting member's own Type I entry sign-off; HTTP-only, never an MCP tool. The decision seals the latest
///     adoption and the gap plans and is acknowledged with 202; <c>ReadinessDecisionProcess</c> records it once every
///     gap plan is held and the single approved-entry slot is still free.
/// </summary>
sealed class DecideTypeIEntryHandler(ReadinessDecisionAdmission admission)
    : IRequestHandler<DecideTypeIEntry, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<DecideTypeIEntry> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result<OperationAcceptedResponse>.Failure(new RequestError(
                RequestErrorKind.Forbidden, "Type I entry sign-off requires personal HTTP submission.")));
        var request = context.Request;
        return admission.AdmitAsync(ReadinessDecisionKind.TypeIEntry, context, request, request.CommandId,
            request.TenantId, request.ProgramId, request.AssessmentId, request.ExpectedRevision, request.Outcome,
            request.Rationale, request.AcknowledgedGapIds ?? [], request.SeparationOfDutiesWaiverId, ct);
    }
}
