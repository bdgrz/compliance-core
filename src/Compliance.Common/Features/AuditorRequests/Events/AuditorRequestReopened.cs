using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     A resolved request was reopened as the root native fact of its operation. The request generation advances and
///     <paramref name="Change" /> is the <c>AuditorRequestReopenContent</c>.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-request-reopened", 1)]
public sealed record AuditorRequestReopened(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
