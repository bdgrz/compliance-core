using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     The approver's personal decision to put a pending operating plan into effect; HTTP-only.
///     <paramref name="CommandId" /> is the logical operation identity. The proposer cannot approve without an
///     approved waiver.
/// </summary>
[Discriminator("bdgrz.control.operating_plan.approve", 2)]
public sealed record ApproveControlOperatingPlan(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, Uuid PlanVersionId, string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ControlOperatingPlanView>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
