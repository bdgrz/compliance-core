using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     Opens a Type II observation period from an approved plan. <paramref name="PeriodId" /> is the logical operation
///     identity, so an exact retry replays the original 202 acknowledgment; the calendar range is acquired and the
///     period applied by <c>TypeIIRangeProcess</c> after the complete opening intent is persisted.
/// </summary>
[Discriminator("bdgrz.operations.start-type-iiobservation-period", 1)]
public sealed record StartTypeIIObservationPeriod(Uuid TenantId, Uuid ProgramId, Uuid PeriodId, Uuid ExaminationId,
    Uuid PlanId, DateOnly PeriodStart, DateOnly PeriodEnd)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => PeriodId;
}
