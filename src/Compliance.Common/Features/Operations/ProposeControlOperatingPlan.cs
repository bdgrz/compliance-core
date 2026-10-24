using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Proposes owner, backup owner, reviewer, cadence, and effective date for the control's exact
///     current approved version. <paramref name="PlanVersionId" /> is the immutable plan version and the logical
///     operation identity, so an exact retry returns the original view; <paramref name="ExpectedRevision" /> is the
///     plan line's logical revision. The plan takes effect only after an independent approval.
/// </summary>
[Discriminator("bdgrz.control.operating_plan.propose", 2)]
public sealed record ProposeControlOperatingPlan(Uuid PlanVersionId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, Uuid ControlVersionId, OperatingHolder Owner, OperatingHolder? BackupOwner,
    Uuid ReviewerMemberId, ControlCadence Cadence, DateOnly EffectiveFrom, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ControlOperatingPlanView>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => PlanVersionId;
}
