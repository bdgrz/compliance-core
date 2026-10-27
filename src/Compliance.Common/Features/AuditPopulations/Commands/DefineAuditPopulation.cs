using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPopulations.Commands;

/// <summary>Defines a population against one exact frozen Type II period close.</summary>
[Discriminator("bdgrz.audit-populations.define-audit-population", 2)]
public sealed record DefineAuditPopulation(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid DefinitionId,
    Uuid PeriodId,
    Uuid PeriodCloseId,
    AuditPopulationDefinitionContent Definition) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
