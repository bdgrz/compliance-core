using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPopulations.Commands;

/// <summary>Records the revise audit population definition intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.revise-audit-population-definition", 2)]
public sealed record ReviseAuditPopulationDefinition(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DefinitionId, long ExpectedRevision, string SourceBoundary, string InclusionRules, string ExclusionRules, string ChangeReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
