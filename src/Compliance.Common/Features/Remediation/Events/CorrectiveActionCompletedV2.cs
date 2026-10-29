using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>
///     The completion of one corrective action on its own owner. <paramref name="EvidenceManifest" /> is the complete
///     evidence list as verified immutable content, with every item and its order preserved.
/// </summary>
[Discriminator("bdgrz.finding.corrective_action.completed", 3)]
public sealed record CorrectiveActionCompletedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid ActionId, string ResolutionNotes, ImmutableContentRef EvidenceManifest,
    Uuid CompletedByMemberId, ActorReference CompletedBy, DateTimeOffset CompletedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
