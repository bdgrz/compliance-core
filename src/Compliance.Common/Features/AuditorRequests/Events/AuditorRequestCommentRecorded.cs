using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The materialization of an admitted comment as its own thread, at revision one. <paramref name="Change" /> repeats
///     the frozen <c>ChildAdmissionIntent</c>; the thread is identified by the comment id and never loads the request.
/// </summary>
[Discriminator("bdgrz.auditorrequests.auditor-request-comment-recorded", 1)]
public sealed record AuditorRequestCommentRecorded(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
