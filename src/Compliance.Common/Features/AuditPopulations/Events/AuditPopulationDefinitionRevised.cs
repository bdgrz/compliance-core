using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records an attributable population-definition revision before population collection.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-definition-revised", 1)]
public sealed record AuditPopulationDefinitionRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPopulationView State,
    DateTimeOffset OccurredAt) : DomainEvent;
