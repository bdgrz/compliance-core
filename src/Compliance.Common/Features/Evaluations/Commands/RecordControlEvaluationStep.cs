using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations.Commands;

/// <summary>
///     Records the evaluator's result for one step. A not_met result needs a deviation classification of minor or
///     material and a description. <paramref name="CommandId" /> is the logical operation identity and
///     <paramref name="ExpectedRevision" /> the revision of this evaluation.
/// </summary>
[Discriminator("bdgrz.control.evaluation.step.record", 2)]
public sealed record RecordControlEvaluationStep(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, Uuid StepId, long ExpectedRevision, string Result, string Rationale,
    IReadOnlyList<EvaluationInspectedItem> InspectedItems, string? DeviationClassification = null,
    string? DeviationDescription = null)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
