using Cntryl.Portia;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.ControlMappings.Queries;

[Discriminator("bdgrz.control_mapping.get", 1)]
public sealed record GetControlCriterionMapping(Uuid TenantId, Uuid ProgramId, Uuid MappingId)
    : IRequest<ControlCriterionMappingView>, IProgramReadRequest, ICallable;
