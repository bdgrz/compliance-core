using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Queries;

/// <summary>Previews a proposed operating plan without recording it.</summary>
[Discriminator("bdgrz.control.operating_plan.preview", 1)]
public sealed record PreviewControlOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ControlVersionId, OperatingHolder Owner, OperatingHolder? BackupOwner,
    Uuid ReviewerMemberId, ControlCadence Cadence, DateOnly EffectiveFrom)
    : IRequest<ControlOperatingPlanPreview>, IProgramScopedRequest, ICallable;
