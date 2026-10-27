using Cntryl.Portia;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.ControlMappings.Queries;

[Discriminator("bdgrz.criterion_applicability.get", 1)]
public sealed record GetCriterionApplicability(Uuid TenantId, Uuid ProgramId, Uuid DecisionId)
    : IRequest<CriterionApplicabilityView>, IProgramReadRequest, ICallable;
