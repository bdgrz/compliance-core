using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Admits an exact-version sample selection linked to the prior admitted selection it supersedes. The earlier
///     selection stays; the new one binds a strictly later version of the same audit population.
/// </summary>
sealed class AmendSampleSelectionHandler(SampleSelectionAdmission admission)
    : IRequestHandler<AmendSampleSelection, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<AmendSampleSelection> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(AuditorCommands.Forbidden<OperationAcceptedResponse>(
                "Amending a sample selection requires a personal HTTP actor."));
        return admission.AdmitAsync(context, actorMemberId, actorDisplay, "AmendSampleSelection",
            AuditorCommands.Digest(request), request.TenantId, request.ProgramId, request.ExaminationId,
            request.AuditorRequestId, request.ExpectedRequestRevision, request.SelectionId,
            request.SupersedesSelectionId, request.PopulationId, request.PopulationVersion, request.PopulationRowIds,
            request.SelectionMethod, request.Source, request.ExternalReference, request.AmendmentReason, ct);
    }
}
