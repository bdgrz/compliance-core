using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Queries;

[Discriminator("bdgrz.criterion_applicability.get", 1)]
public sealed record GetCriterionApplicability(Uuid TenantId, Uuid ProgramId, Uuid DecisionId)
    : IRequest<CriterionApplicabilityView>, IProgramReadRequest, ICallable;
