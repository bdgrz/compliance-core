using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     A gap plan was recorded as the root native fact of its operation. <c>Revision</c> is the gap plan revision after
///     this plan and <c>PlanContent</c> the complete <see cref="ReadinessGapPlanView" />.
/// </summary>
[Discriminator("bdgrz.readiness.gap.planned", 1)]
public sealed record ReadinessGapPlanned(Uuid TenantId, Uuid ProgramId, long Revision,
    ImmutableContentRef PlanContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
