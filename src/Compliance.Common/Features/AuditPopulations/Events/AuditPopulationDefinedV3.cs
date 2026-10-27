using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AuditPopulations.Events;

/// <summary>Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-defined", 3)]
public sealed record AuditPopulationDefinedV3(Uuid TenantId, Uuid ProgramId, Uuid SubjectId, AuditPopulationHeader Header, ImmutableContentRef CurrentContent)
    : DomainEvent, IAuditPopulationStateFact;
