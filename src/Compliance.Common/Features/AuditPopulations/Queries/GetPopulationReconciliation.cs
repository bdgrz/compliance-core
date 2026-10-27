using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPopulations.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-populations.get-population-reconciliation", 1)]
public sealed record GetPopulationReconciliation(Uuid TenantId, Uuid ProgramId, Uuid PopulationReconciliationId)
    : IRequest<PopulationReconciliationView>, IProgramReadRequest, ICallable;
