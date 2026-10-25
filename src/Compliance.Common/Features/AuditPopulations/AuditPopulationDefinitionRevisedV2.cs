using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags.</summary>
[Discriminator("bdgrz.auditpopulations.audit-population-definition-revised", 2)]
public sealed record AuditPopulationDefinitionRevisedV2(Uuid TenantId, Uuid ProgramId, Uuid SubjectId, AuditPopulationHeader Header, ImmutableContentRef CurrentContent, ImmutableContentRef Command, ImmutableContentRef SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IAuditPopulationNativeFact;
