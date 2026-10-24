using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The request was resolved by the exact pending resolution. <paramref name="Change" /> is the frozen
///     <c>ClosureIntent</c> of the resolution, <paramref name="ResponseDispositionProofs" /> the complete manifest of the
///     held accepted dispositions of every admitted response and <paramref name="RequestGeneration" /> the generation
///     the resolution sealed.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-request-resolved", 1)]
public sealed record AuditorRequestResolved(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation, long RequestGeneration,
    ImmutableContentRef ResponseDispositionProofs, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
