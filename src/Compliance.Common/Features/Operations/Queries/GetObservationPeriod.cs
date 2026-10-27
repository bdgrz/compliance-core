using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.operations.get-observation-period", 1)]
public sealed record GetObservationPeriod(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId)
    : IRequest<ObservationPeriodView>, IProgramReadRequest, ICallable;
