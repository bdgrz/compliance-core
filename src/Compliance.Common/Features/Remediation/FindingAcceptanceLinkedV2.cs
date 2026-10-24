using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>A risk acceptance or waiver linked to the finding; the complete <see cref="FindingAcceptanceView" /> is verified content.</summary>
[Discriminator("bdgrz.finding.acceptance.linked", 3)]
public sealed record FindingAcceptanceLinkedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, ImmutableContentRef AcceptanceContent, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
