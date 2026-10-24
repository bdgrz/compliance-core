using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     The evaluator's personal sign-off of separate design, implementation, and evidence sufficiency conclusions;
///     HTTP-only. The overall result is derived, never supplied. <paramref name="CommandId" /> is the logical
///     operation identity and <paramref name="ExpectedRevision" /> the revision of this evaluation.
/// </summary>
[Discriminator("bdgrz.control.evaluation.submit", 2)]
public sealed record SubmitControlEvaluation(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long ExpectedRevision, IReadOnlyList<EvaluationAssertionConclusion> Conclusions)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
