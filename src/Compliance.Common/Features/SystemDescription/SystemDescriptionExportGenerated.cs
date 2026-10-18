using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a report-ready export identity tied to an approved version.</summary>
[Discriminator("bdgrz.systemdescription.system-description-export-generated", 1)]
public sealed record SystemDescriptionExportGenerated(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionExportView Export,
    DateTimeOffset OccurredAt) : DomainEvent;
