using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPopulations.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AuditPopulations.Events;

/// <summary>Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-fact-recorded", 3)]
public sealed record AuditPopulationFactRecordedV3(Uuid TenantId, Uuid ProgramId, Uuid SubjectId, AuditPopulationHeader Header, ImmutableContentRef CurrentContent, ImmutableContentRef Command, ImmutableContentRef SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IAuditPopulationNativeFact;
