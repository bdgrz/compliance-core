using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Admits an auditor's selection of exact frozen population rows on the open request. The request revision is
///     compared on the request; the independent selection owner is then created at its own first revision.
/// </summary>
sealed class RecordSampleSelectionHandler(SampleSelectionAdmission admission)
    : IRequestHandler<RecordSampleSelection, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<RecordSampleSelection> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(AuditorCommands.Forbidden<OperationAcceptedResponse>(
                "Recording a sample selection requires a personal HTTP actor."));
        return admission.AdmitAsync(context, actorMemberId, actorDisplay, "RecordSampleSelection",
            AuditorCommands.Digest(request), request.TenantId, request.ProgramId, request.ExaminationId,
            request.AuditorRequestId, request.ExpectedRequestRevision, request.SelectionId, null, request.PopulationId,
            request.PopulationVersion, request.PopulationRowIds, request.SelectionMethod, request.Source,
            request.ExternalReference, null, ct);
    }
}
