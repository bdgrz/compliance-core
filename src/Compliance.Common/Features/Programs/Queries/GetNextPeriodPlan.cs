using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.programs.get-next-period-plan", 1)]
public sealed record GetNextPeriodPlan(Uuid TenantId, Uuid ProgramId, Uuid NextPeriodPlanId)
    : IRequest<NextPeriodPlanView>, IProgramReadRequest, ICallable;
