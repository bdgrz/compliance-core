using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Queries;

[Discriminator("bdgrz.control.operating_plan.get", 1)]
public sealed record GetControlOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid ControlId)
    : IRequest<ControlOperatingPlanSetView>, IProgramReadRequest, ICallable;
