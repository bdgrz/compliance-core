using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

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
