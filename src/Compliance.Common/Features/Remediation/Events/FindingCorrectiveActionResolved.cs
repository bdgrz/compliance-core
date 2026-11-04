using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>The finding recorded the exact completed or cancelled outcome of one action it planned.</summary>
[Discriminator("bdgrz.finding.corrective_action.resolved", 1)]
public sealed record FindingCorrectiveActionResolved(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid ActionId, string Outcome, NativeFactReference Source, ImmutableContentRef SourceProof,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse = null, ImmutableContentRef? OperationResult = null)
    : DomainEvent, INativeOperationFact;
