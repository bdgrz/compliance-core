using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Events;

/// <summary>Records a population definition with its original command and response receipt.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-defined", 4)]
public sealed record AuditPopulationDefinedV4(Uuid TenantId, Uuid ProgramId, Uuid SubjectId,
    AuditPopulationHeader Header, ImmutableContentRef CurrentContent, ImmutableContentRef Command,
    ImmutableContentRef SourceProofs, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, IAuditPopulationNativeFact;
