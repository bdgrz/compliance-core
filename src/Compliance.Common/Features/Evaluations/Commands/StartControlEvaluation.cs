using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Starts an evaluation against one exact immutable control-evaluation plan version.
///     <paramref name="EvaluationId" /> is the identity of the new evaluation and the logical operation identity, so an
///     exact retry returns the original view. A retest names the accepted evaluation it retests and may use that
///     evaluation's plan version or a successor; it is a new evaluation and never changes its predecessor.
/// </summary>
[Discriminator("bdgrz.control.evaluation.start", 2)]
public sealed record StartControlEvaluation(Uuid EvaluationId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid PlanVersionId, Uuid? RetestOfEvaluationId = null)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => EvaluationId;
}
