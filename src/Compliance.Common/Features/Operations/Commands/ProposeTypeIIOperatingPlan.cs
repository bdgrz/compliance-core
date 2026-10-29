using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>Proposes a complete Type II plan tied to one examination and exact source versions.</summary>
[Discriminator("bdgrz.operations.propose-type-iioperating-plan", 2)]
public sealed record ProposeTypeIIOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid PlanId,
    Uuid ExaminationId, TypeIIPlanContent Content)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
