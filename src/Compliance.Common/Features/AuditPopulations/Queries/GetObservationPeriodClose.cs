using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPopulations.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-populations.get-observation-period-close", 1)]
public sealed record GetObservationPeriodClose(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodCloseId)
    : IRequest<ObservationPeriodCloseView>, IProgramReadRequest, ICallable;
