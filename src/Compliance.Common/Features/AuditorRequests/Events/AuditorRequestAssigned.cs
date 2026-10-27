using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     An open request was assigned. <paramref name="Change" /> is the complete <c>AuditorRequestTermsContent</c> after
///     the assignment.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-request-assigned", 1)]
public sealed record AuditorRequestAssigned(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
