using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     An independent approval that puts a pending plan version into effect. The complete list of open work that
///     moves to the approved owner is verified immutable content, so no item can be truncated.
/// </summary>
[Discriminator("bdgrz.control.operating_plan.approved", 2)]
public sealed record ControlOperatingPlanApproved(Uuid TenantId, Uuid ProgramId, Uuid ControlId, long Revision,
    Uuid PlanVersionId, Uuid ApproverMemberId, ActorReference ApprovedBy, DateTimeOffset ApprovedAt,
    string Rationale, Uuid? SeparationOfDutiesWaiverId, ImmutableContentRef ReassignmentsManifest,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
