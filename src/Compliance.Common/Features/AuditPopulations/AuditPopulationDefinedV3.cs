using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-defined", 3)]
public sealed record AuditPopulationDefinedV3(Uuid TenantId, Uuid ProgramId, Uuid SubjectId, AuditPopulationHeader Header, ImmutableContentRef CurrentContent)
    : DomainEvent, IAuditPopulationStateFact;
