using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
