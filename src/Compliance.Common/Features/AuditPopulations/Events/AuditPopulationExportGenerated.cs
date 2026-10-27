using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.AuditPopulations.Events;

/// <summary>Records the AuditPopulationExportGenerated domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-export-generated", 2)]
public sealed record AuditPopulationExportGenerated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPopulationView State,
    DateTimeOffset OccurredAt) : DomainEvent;
