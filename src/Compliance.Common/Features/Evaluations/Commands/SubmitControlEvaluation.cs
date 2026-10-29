using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
namespace Bdgrz.Compliance.Features.Evaluations.Commands;

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
