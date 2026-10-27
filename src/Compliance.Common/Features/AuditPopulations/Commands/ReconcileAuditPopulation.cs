using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPopulations.Commands;

/// <summary>Records the reconcile audit population intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.reconcile-audit-population", 3)]
public sealed record ReconcileAuditPopulation(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PopulationId, long ExpectedRevision, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
