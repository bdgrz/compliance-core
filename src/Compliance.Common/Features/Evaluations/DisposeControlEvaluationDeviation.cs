using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

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
