using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Admits a rule-based preview on the service-epoch owner of the client and acknowledges it with a fixed 202.
///     The preview never supplies partner sign-off or grants client access. <paramref name="EvaluationId" /> is the
///     logical operation identity; <paramref name="ExpectedServiceEpoch" /> is the current service epoch, never the
///     revision of the preview that is materialized later.
/// </summary>
[Discriminator("bdgrz.independence.evaluate", 1)]
public sealed record EvaluateClientIndependence(Uuid TenantId, Uuid EvaluationId, long RuleSetVersion,
    DateOnly ExaminationPeriodStart, long ExpectedServiceEpoch)
    : IRequest<OperationAcceptedResponse>, IIndependenceAdministrationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => EvaluationId;
}
