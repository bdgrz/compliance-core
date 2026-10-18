using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Proposes a complete Type II plan tied to one examination and exact source versions.</summary>
[Discriminator("bdgrz.operations.propose-type-iioperating-plan", 2)]
public sealed record ProposeTypeIIOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid PlanId,
    Uuid ExaminationId, TypeIIPlanContent Content)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
