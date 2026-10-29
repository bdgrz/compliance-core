using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     Amends the date range of an open Type II period. <paramref name="CommandId" /> is the logical operation
///     identity and the amendment identity; <paramref name="ExpectedRevision" /> is the period's logical revision.
///     The old and the new range are both held by the calendar until the period applies the amendment.
/// </summary>
[Discriminator("bdgrz.operations.amend-type-iiobservation-period", 1)]
public sealed record AmendTypeIIObservationPeriod(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PeriodId,
    long ExpectedRevision, DateOnly? PeriodStart, DateOnly? PeriodEnd, string AmendmentReason)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
