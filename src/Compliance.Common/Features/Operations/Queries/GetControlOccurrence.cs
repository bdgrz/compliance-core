using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Queries;

[Discriminator("bdgrz.control.occurrence.get", 1)]
public sealed record GetControlOccurrence(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId)
    : IRequest<ControlOccurrenceView>, IProgramReadRequest, ICallable;
