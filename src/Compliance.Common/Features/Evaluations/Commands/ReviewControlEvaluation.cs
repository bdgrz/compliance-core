using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Commands;

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
