using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records an attributed source snapshot and its complete, individually inspectable rows.</summary>
[Discriminator("bdgrz.audit-populations.record-audit-population-facts", 3)]
public sealed record RecordAuditPopulationFacts(Uuid CommandId, 
    Uuid TenantId,
    Uuid ProgramId,
    Uuid DefinitionId,
    long ExpectedRevision,
    AuditPopulationSourceSnapshotContent SourceSnapshot,
    IReadOnlyList<AuditPopulationRowContent> Rows) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
