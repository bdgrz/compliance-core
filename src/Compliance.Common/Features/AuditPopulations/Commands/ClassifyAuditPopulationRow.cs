using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPopulations.Commands;

/// <summary>Records the classify audit population row intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.classify-audit-population-row", 2)]
public sealed record ClassifyAuditPopulationRow(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PopulationId, Uuid RowId, string Classification, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
