using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.AuditPopulations.Events;

/// <summary>Records an attributable population-definition revision before population collection.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-definition-revised", 1)]
public sealed record AuditPopulationDefinitionRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditPopulationView State,
    DateTimeOffset OccurredAt) : DomainEvent;
