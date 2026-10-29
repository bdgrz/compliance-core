using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
namespace Bdgrz.Compliance.Features.Evaluations.Commands;

/// <summary>
///     Records a minor deviation's disposition: corrected, or accepted_with_waiver naming an approved waiver.
///     <paramref name="CommandId" /> is the logical operation identity and <paramref name="ExpectedRevision" /> the
///     revision of this evaluation.
/// </summary>
[Discriminator("bdgrz.control.evaluation.deviation.dispose", 2)]
public sealed record DisposeControlEvaluationDeviation(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, Uuid DeviationId, long ExpectedRevision, string Disposition, string Rationale,
    Uuid? WaiverId = null)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
