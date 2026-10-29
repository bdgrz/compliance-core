using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>A risk acceptance or waiver linked to the finding; the complete <see cref="FindingAcceptanceView" /> is verified content.</summary>
[Discriminator("bdgrz.finding.acceptance.linked", 3)]
public sealed record FindingAcceptanceLinkedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, ImmutableContentRef AcceptanceContent, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
