using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The materialization of an admitted response draft on its own owner, at revision one. <paramref name="Change" />
///     repeats the frozen <c>ChildAdmissionIntent</c> the request accepted; the response never loads the live request.
///     The actor is the original author of the draft.
/// </summary>
[Discriminator("bdgrz.auditorrequests.audit-response-draft-created", 1)]
public sealed record AuditResponseDraftCreated(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
