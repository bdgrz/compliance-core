using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
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
