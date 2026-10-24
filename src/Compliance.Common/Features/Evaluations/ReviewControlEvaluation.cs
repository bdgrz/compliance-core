using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     The independent reviewer's personal decision on the submitted round; HTTP-only. Only a program manager who is
///     not the evaluator may review, unless an approved waiver applies. <paramref name="CommandId" /> is the logical
///     operation identity and <paramref name="ExpectedRevision" /> the revision of this evaluation.
/// </summary>
[Discriminator("bdgrz.control.evaluation.review", 2)]
public sealed record ReviewControlEvaluation(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid EvaluationId, long ExpectedRevision, string Decision, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ControlEvaluationView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest,
        ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
