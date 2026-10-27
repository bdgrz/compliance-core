using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     A pending plan version on one control's plan line. The logical id of <paramref name="Operation" /> is the
///     immutable plan version id; the complete <see cref="ControlOperatingPlanView" /> is verified immutable content.
/// </summary>
[Discriminator("bdgrz.control.operating_plan.proposed", 2)]
public sealed record ControlOperatingPlanProposed(Uuid TenantId, Uuid ProgramId, Uuid ControlId, long Revision,
    ImmutableContentRef PlanContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
