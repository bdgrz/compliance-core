using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPopulations.Commands;

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
